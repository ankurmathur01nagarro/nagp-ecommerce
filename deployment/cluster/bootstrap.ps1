[CmdletBinding()]
param()

# Import all required modules
Import-Module -Name ".\setup-wizard.psm1" -Force -DisableNameChecking
Import-Module -Name ".\error-handling.psm1" -Force -DisableNameChecking
Import-Module -Name ".\logging.psm1" -Force -DisableNameChecking
Import-Module -Name ".\idempotency.psm1" -Force -DisableNameChecking

# Initialize logging first
try {
    $logsDir = Join-Path (Get-Location) "logs"
    $null = Initialize-Logging -LogDirectory $logsDir
    Log-Info "═════════════════════════════════════════════════════════════"
    Log-Info "Kubernetes Cluster Setup Script Started"
    Log-Info "═════════════════════════════════════════════════════════════"
} catch {
    Write-Host "[ERROR] Failed to initialize logging: $_" -ForegroundColor Red
    exit 1
}

# Initialize Spectre.Console and Deployment Context
try {
    Initialize-SpectreConsole
    $ctx = Get-DeploymentContext
    Log-Info "Spectre.Console and Deployment Context initialized"
} catch {
    Log-Error "Failed to initialize: $_"
    exit 1
}

# Install required tools
Show-SectionHeader "Installing Tools: kubectl, Istioctl, argocd CLI, helm"

$ctx.ExecuteStep("Installing Required Tools", {
    $installedTools = @()
    @("kubectl", "istioctl", "argocd", "helm") | ForEach-Object {
        if (Test-CommandExists -Command $_) {
            Log-Info "$_ already installed"
        } else {
            scoop install $_
        }
        $installedTools += $_
    }
    Show-Success "Tools installed: $($installedTools -join ', ')"
})

# Add Helm repos
$ctx.ExecuteStep("Adding Helm Repositories", {
    helm repo add argo https://argoproj.github.io/argo-helm
    helm repo add external-secrets https://charts.external-secrets.io
    helm repo add hashicorp https://helm.releases.hashicorp.com
    helm repo add jetstack https://charts.jetstack.io
    helm repo add stakater https://stakater.github.io/stakater-charts
    helm repo update
})

# ============================================================================
# Interactive Setup Wizard - Input Collection Phase
# ============================================================================
Log-StepStart "Collecting Configuration from User"

try {
    Show-WizardHeader

    # Step 1: Cluster Selection
    $kubeContext = Select-KubernetesContext
    if ($null -eq $kubeContext) {
        Log-Warning "User cancelled cluster context selection"
        Show-CancelledMessage
        exit 0
    }
    
    Write-Host ""
    $proceed = Confirm-ClusterSelection -ClusterType $clusterType -Context $kubeContext
    if (-not $proceed) {
        Log-Warning "User cancelled cluster confirmation"
        Show-CancelledMessage
        exit 0
    }
    
    Log-Info "Switching to Kubernetes context: $kubeContext"
    Invoke-CommandWithRetry -ScriptBlock {
        kubectl config use-context $kubeContext
    } -Description "Switch Kubernetes context"
    
    Write-Host ""
    $kubernetesPlatform = Get-KubernetesPlatform
    $istioPlatform = Get-IstioPlatformValue -Platform $kubernetesPlatform
    Write-Host ""
    Log-Info "Selected platform: $kubernetesPlatform (Istio platform: $istioPlatform)"
    
    Write-Host ""

    # Step 2: Security Configuration
    $ArgoCDAdminPassword = Get-ArgoCDPassword
    $GrafanaAdminPassword = Get-GrafanaPassword
    $NewRelicApiKey = Get-NewRelicApiKey
    $InstallDemocraticCsi = Get-DemocraticCsiOption
    $DemocraticCsiConfig = $null
    if ($InstallDemocraticCsi) {
        Write-SpectreHost "[yellow]TrueNAS Configuration for democratic-csi[/]"
        Write-SpectreHost ""

        $truenasIp = Read-SpectreText -Prompt "Enter TrueNAS IP address"
        $truenasApiKey = Read-SpectreText -Prompt "Enter TrueNAS API key" -Secret
        $truenasNetworkCidr = Read-SpectreText -Prompt "Enter allowed network CIDR (e.g. 192.168.1.0/24)"
        $nfsDatasetParent = Read-SpectreText -Prompt "Enter NFS ZFS dataset parent (e.g. main/k3s/nfs)"
        $iscsiDatasetParent = Read-SpectreText -Prompt "Enter iSCSI ZFS dataset parent (e.g. main/k3s/iscsi)"

        Write-SpectreHost ""

        $DemocraticCsiConfig = @{
            TrueNasIp          = $truenasIp
            TrueNasApiKey       = $truenasApiKey
            TrueNasNetworkCidr  = $truenasNetworkCidr
            NfsDatasetParent    = $nfsDatasetParent
            IscsiDatasetParent  = $iscsiDatasetParent
        }
    }
    Show-ConfigurationSummary -ArgoCDPassword $ArgoCDAdminPassword -GrafanaPassword $GrafanaAdminPassword -NewRelicApiKey $NewRelicApiKey -InstallDemocraticCsi $InstallDemocraticCsi

    $proceed = Confirm-Setup
    if (-not $proceed) {
        Log-Warning "User cancelled setup confirmation"
        Show-CancelledMessage
        exit 0
    }

    Log-StepComplete "Configuration collection completed"
    Show-StartMessage

} catch {
    Log-StepFailed "Configuration collection failed: $_"
    exit 1
}

Show-SectionHeader "Using Remote Kubernetes Cluster"  
$ctx.ExecuteStep("Verifying Remote Cluster Access", {
    kubectl cluster-info
    Show-Success "Connected to remote cluster"
})

# Install Gateway API CRDs
Show-SectionHeader "Install Gateway API, ArgoCD CRDs"

$ctx.ExecuteStep("Installing Gateway API CRDs", {
    if (-not (Test-GatewayAPICRDsInstalled)) {
        kubectl apply --server-side -f "https://github.com/kubernetes-sigs/gateway-api/releases/download/v1.4.1/standard-install.yaml"
        $ctx.AutoTrack("KubernetesCRD", "gateway-api")
    } else {
        Log-Info "Gateway API CRDs already installed"
    }
})

# Install Istio
Show-SectionHeader "Install Istio"

$ctx.ExecuteStep("Installing Istio", {
    if (-not (Test-IstioInstalled)) {
        if ($istioPlatform -notin @("generic", "talos")) {
            istioctl install -f ..\scripts\istio-config.yaml --set "values.global.platform=$istioPlatform" -y
        } else {
            kubectl create namespace istio-system
            kubectl label namespace istio-system `
                pod-security.kubernetes.io/enforce=privileged `
                pod-security.kubernetes.io/audit=privileged `
                pod-security.kubernetes.io/warn=privileged `
                --overwrite
            istioctl install -f ..\scripts\istio-config.yaml -y
        }
        Wait-IstioPodsReady
        kubectl apply -f https://raw.githubusercontent.com/istio/istio/release-1.28/samples/addons/kiali.yaml
        $ctx.AutoTrack("ServiceMesh", "istio")
        Show-Success "Istio installed with platform: $kubernetesPlatform"
    } else {
        Log-Info "Istio already installed, skipping"
    }
})

# Prepare namespaces and secrets
Show-SectionHeader "Pre-create namespaces and secrets (required before ArgoCD syncs)"

$ctx.ExecuteStep("Creating Namespaces and Secrets", {
    # Create observability namespace
    if (-not (Test-NamespaceExists -Namespace "observability")) {
        kubectl create namespace observability
        $ctx.AutoTrack("Namespace", "observability")
    } else {
        Log-Info "Namespace 'observability' already exists"
    }

    # Create nagp-ecom namespace
    if (-not (Test-NamespaceExists -Namespace "nagp-ecom")) {
        kubectl create namespace nagp-ecom
        $ctx.AutoTrack("Namespace", "nagp-ecom")
    } else {
        Log-Info "Namespace 'nagp-ecom' already exists"
    }

    # Create New Relic secret for OpenTelemetry Collector
    if (-not [string]::IsNullOrWhiteSpace($NewRelicApiKey)) {
        if (-not (Test-SecretExists -Namespace "observability" -SecretName "newrelic-otel-secret")) {
            kubectl create secret generic newrelic-otel-secret `
                --from-literal=api-key=$NewRelicApiKey `
                -n observability
            $ctx.AutoTrack("Secret", "newrelic-otel-secret", "observability")
            Show-Success "New Relic secret created"
        } else {
            Log-Info "New Relic secret already exists"
        }
    } else {
        Log-Info "Skipping New Relic secret (no API key provided)"
    }

    # Create Grafana admin secret
    if (-not (Test-SecretExists -Namespace "observability" -SecretName "grafana-admin-secret")) {
        kubectl create secret generic grafana-admin-secret `
            --from-literal=admin-user=admin `
            --from-literal=admin-password=$GrafanaAdminPassword `
            -n observability
        $ctx.AutoTrack("Secret", "grafana-admin-secret", "observability")
        Show-Success "Grafana secret created"
    } else {
        Log-Info "Grafana secret already exists"
    }

    # Azure Service Principal Secret
    Write-SpectreHost "[yellow]Azure Service Principal Setup[/]"
    $az_clientId = Read-SpectreText -Prompt "Enter ClientId"
    $az_clientSecret = Read-SpectreText -Prompt "Enter ClientSecret" -Secret

    kubectl create secret generic secret-azure-sp `
        --from-literal=ClientID="$az_clientId" `
        --from-literal=ClientSecret="$az_clientSecret" `
        -n nagp-ecom
})

# Install cert-manager
# Must be installed before ArgoCD applications are deployed — the cert-manager-webhook-duckdns
# ArgoCD app (sync wave 1) assumes cert-manager CRDs and controllers are already present.
Show-SectionHeader "Install cert-manager"
$ctx.ExecuteStep("Installing cert-manager", {
    if (-not (Test-NamespaceExists -Namespace "cert-manager")) {
        helm upgrade --install cert-manager jetstack/cert-manager `
            --namespace cert-manager `
            --create-namespace `
            --set crds.enabled=true
        $ctx.AutoTrack("HelmRelease", "cert-manager", "cert-manager")
        Show-Success "cert-manager installed"
    } else {
        Log-Info "cert-manager already installed, skipping"
    }
})

# Install Reloader
# Watches for ConfigMap/Secret changes and automatically restarts Deployments in
# namespaces labelled reloader-enabled=true — no per-resource annotations needed.
Show-SectionHeader "Install Reloader"
$ctx.ExecuteStep("Installing Reloader", {
    if (-not (Test-NamespaceExists -Namespace "reloader")) {
        helm upgrade --install reloader stakater/reloader `
            --namespace reloader `
            --create-namespace `
            --set reloader.autoReloadAll=true `
            --set "reloader.namespaceSelector=reloader-enabled=true"
        $ctx.AutoTrack("HelmRelease", "reloader", "reloader")
        kubectl label ns nagp-ecom "reloader-enabled=true"
        Show-Success "Reloader installed"
    } else {
        Log-Info "Reloader already installed, skipping"
    }
})

# Install External Secrets Operator
Show-SectionHeader "Install External Secrets Operator"
$ctx.ExecuteStep("Installing External Secrets Operator", {
    helm upgrade --install external-secrets external-secrets/external-secrets `
        --namespace external-secrets `
        --create-namespace `
        --set installCRDs=true
    
    $ctx.AutoTrack("HelmRelease", "external-secrets", "external-secrets")
    Show-Success "External Secrets Operator installed"
})

# Install ArgoCD
Show-SectionHeader "Install ArgoCD (with Application health check for sync waves)"

$ctx.ExecuteStep("Installing ArgoCD", {
    if (-not (Test-ArgoCDInstalled)) {
        if (-not (Test-NamespaceExists -Namespace "argocd")) {
            kubectl create namespace argocd
            $ctx.AutoTrack("Namespace", "argocd")
        }
        
        # Generate bcrypt hash of the admin password
        $bcryptPassword = argocd account bcrypt --password $ArgoCDAdminPassword
        
        helm install argocd argo/argo-cd `
            -n argocd `
            -f ..\scripts\helm-argocd-values.yaml `
            --set "configs.secret.argocdServerAdminPassword=$bcryptPassword"
        
        $ctx.AutoTrack("HelmRelease", "argocd", "argocd")
        Wait-ArgoCDPodsReady
        Show-Success "ArgoCD installed"
    } else {
        Log-Info "ArgoCD already installed, skipping installation"
    }
})

Write-Host ""
Show-Warning "Access ArgoCD UI"
Show-SectionHeader "kubectl port-forward service/argocd-server -n argocd 8080:443"

# Install democratic-csi storage driver (conditional)
if ($InstallDemocraticCsi) {
    Show-SectionHeader "Install democratic-csi Storage Driver (TrueNAS)"

    $ctx.ExecuteStep("Generating democratic-csi config files", {
        $overlayPath = Join-Path (Get-Location) "..\scripts\democratic-csi\overlays\local-truenas"
        
        $replacements = @{
            '{TRUENAS_IP}'           = $DemocraticCsiConfig.TrueNasIp
            '{TRUENAS_API_KEY}'      = $DemocraticCsiConfig.TrueNasApiKey
            '{TRUENAS_NETWORK_CIDR}' = $DemocraticCsiConfig.TrueNasNetworkCidr
            '{NFS_DATASET_PARENT}'   = $DemocraticCsiConfig.NfsDatasetParent
            '{ISCSI_DATASET_PARENT}' = $DemocraticCsiConfig.IscsiDatasetParent
        }

        foreach ($tplFile in Get-ChildItem -Path $overlayPath -Filter "*.yaml.tpl") {
            $outputFile = $tplFile.FullName -replace '\.tpl$', ''
            $content = Get-Content -Path $tplFile.FullName -Raw
            foreach ($key in $replacements.Keys) {
                $content = $content.Replace($key, $replacements[$key])
            }
            Set-Content -Path $outputFile -Value $content -NoNewline
            Show-Success "Generated $(Split-Path $outputFile -Leaf)"
        }
    })

    $ctx.ExecuteStep("Installing democratic-csi", {
        kustomize build ..\scripts\democratic-csi\overlays\local-truenas\ --enable-helm | kubectl apply -f -
        $ctx.AutoTrack("KubernetesResource", "democratic-csi")
        Show-Success "democratic-csi installed"
    })
}

# Install Applications
Show-SectionHeader "Install ArgoCD Application that contains all (Apps of App Pattern)"

$ctx.ExecuteStep("Deploying Applications via ArgoCD", {
    kubectl label ns nagp-ecom "istio.io/dataplane-mode=ambient"
    
    kubectl apply -f ..\scripts\application.yaml
    $ctx.AutoTrack("ArgoCDApplication", "nagp-applications")
})

Log-Success "Cluster deployment completed successfully"
Show-CompletionSummary

# ============================================================================
# Completion
# ============================================================================
Log-Info "═════════════════════════════════════════════════════════════"
Log-Info "Kubernetes Cluster Setup Completed Successfully"
Log-Info "═════════════════════════════════════════════════════════════"
Display-LogSummary
Show-LogLocation
