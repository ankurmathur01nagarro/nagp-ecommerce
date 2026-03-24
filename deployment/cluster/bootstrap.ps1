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
        -n default
})

# Install External Secrets Operator
Show-SectionHeader "Install External Secrets Operator"
$ctx.ExecuteStep("Installing External Secrets Operator", {
    helm install external-secrets external-secrets/external-secrets `
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
    $ctx.ExecuteStep("Installing democratic-csi", {
        kustomize build ..\scripts\democratic-csi\overlays\local-truenas\ --enable-helm | kubectl apply -f -
        $ctx.AutoTrack("KubernetesResource", "democratic-csi")
        Show-Success "democratic-csi installed"
    })
}

# Install Applications
Show-SectionHeader "Install ArgoCD Application that contains all (Apps of App Pattern)"

$ctx.ExecuteStep("Deploying Applications via ArgoCD", {
    # Create namespace for application with istio ambient mode labels
    if (-not (Test-NamespaceExists -Namespace "nagp-ecom")) {
        kubectl create namespace nagp-ecom
        $ctx.AutoTrack("Namespace", "nagp-ecom")
        kubectl label ns nagp-ecom "istio.io/dataplane-mode=ambient"
    } else {
        Log-Info "Namespace 'nagp-ecom' already exists"
    }
    
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
