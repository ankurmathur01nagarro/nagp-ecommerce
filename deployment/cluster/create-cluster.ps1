[CmdletBinding()]
param(
    [switch]$DryRun
)

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

# ============================================================================
# Interactive Setup Wizard - Input Collection Phase
# ============================================================================
Log-StepStart "Collecting Configuration from User"

try {
    Show-WizardHeader

    # Step 1: Cluster Selection
    $clusterType = Get-KubernetesClusterType
    Write-Host ""
    Log-Info "User selected cluster type: $clusterType"

    if ($clusterType -eq "remote") {
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
        $useK3d = $false
    } else {
        $proceed = Confirm-ClusterSelection -ClusterType $clusterType
        if (-not $proceed) {
            Log-Warning "User cancelled cluster confirmation"
            Show-CancelledMessage
            exit 0
        }
        
        $useK3d = $true
        $kubernetesPlatform = "k3d"
        $istioPlatform = "k3d"
        Log-Info "Local k3d cluster selected"
    }

    Write-Host ""

    # Step 2: Security Configuration
    $ArgoCDAdminPassword = Get-ArgoCDPassword
    $GrafanaAdminPassword = Get-GrafanaPassword
    $NewRelicApiKey = Get-NewRelicApiKey
    Show-ConfigurationSummary -ArgoCDPassword $ArgoCDAdminPassword -GrafanaPassword $GrafanaAdminPassword -NewRelicApiKey $NewRelicApiKey

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

# ============================================================================
# Cluster Creation Phase
# ============================================================================

try {
if ($useK3d) {
    Show-SectionHeader "Creating Local Kubernetes Cluster (k3d)"
    
    $ctx.ExecuteStep("Creating Local Kubernetes Cluster (k3d)", {
        if (-not (Test-K3dClusterExists -ClusterName "local")) {
            k3d cluster create local `
                --agents 2 `
                --port "80:80@server:0" `
                --port "443:443@server:0" `
                --port "8000:8000@server:0" `
                --k3s-arg "--disable=traefik@server:0" `
                --api-port 6555
            
            kubectl config use-context k3d-local
            $ctx.AutoTrack("K3dCluster", "local")
            Show-Success "Local k3d cluster created and configured"
        } else {
            Log-Info "k3d cluster 'local' already exists"
        }
    })
} else {
    Show-SectionHeader "Using Remote Kubernetes Cluster"
    
    $ctx.ExecuteStep("Verifying Remote Cluster Access", {
        kubectl cluster-info
        Show-Success "Connected to remote cluster"
    })
}

# Install required tools
Show-SectionHeader "Installing Tools: Istioctl, argocd CLI, helm"

$ctx.ExecuteStep("Installing Required Tools", {
    $installedTools = @()
    @("istioctl", "argocd", "helm") | ForEach-Object {
        if (Test-CommandExists -Command $_) {
            Log-Info "$_ already installed"
        } else {
            scoop install $_
        }
        $installedTools += $_
    }
    Show-Success "Tools installed: $($installedTools -join ', ')"
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

# Add Helm repo
$ctx.ExecuteStep("Adding Helm Repositories", {
    helm repo add argo https://argoproj.github.io/argo-helm
    helm repo update
})

# Install Istio
Show-SectionHeader "Install Istio"

$ctx.ExecuteStep("Installing Istio", {
    if (-not (Test-IstioInstalled)) {
        istioctl install -f ..\deployment\scripts\istio-config.yaml --set "values.global.platform=$istioPlatform" -y
        kubectl apply -f https://raw.githubusercontent.com/istio/istio/release-1.28/samples/addons/kiali.yaml
        Wait-IstioPodsReady
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
        
        if ($DryRun) {
            Log-Warning "DRY RUN: Would install ArgoCD with bcrypt password"
        } else {
            helm install argocd argo/argo-cd `
                -n argocd `
                -f ..\deployment\scripts\helm-argocd-values.yaml `
                --set "configs.secret.argocdServerAdminPassword=$bcryptPassword"
            
            $ctx.AutoTrack("HelmRelease", "argocd", "argocd")
            Wait-ArgoCDPodsReady
            Show-Success "ArgoCD installed"
        }
    } else {
        Log-Info "ArgoCD already installed, skipping installation"
    }
})

Write-Host ""
Show-Warning "Access ArgoCD UI"
Show-SectionHeader "kubectl port-forward service/argocd-server -n argocd 8080:443"

# Install Applications
Show-SectionHeader "Install ArgoCD Application that contains all (Apps of App Pattern)"

$ctx.ExecuteStep("Deploying Applications via ArgoCD", {
    # Create namespace for application with istio ambient mode labels
    if (-not (Test-NamespaceExists -Namespace "nagp-ecom")) {
        kubectl create namespace nagp-ecom `
            --labels "istio.io/dataplane-mode=ambient"
        $ctx.AutoTrack("Namespace", "nagp-ecom")
    } else {
        Log-Info "Namespace 'nagp-ecom' already exists"
    }
    
    if (-not (Test-ArgoCDApplicationsExist)) {
        kubectl apply -f ..\deployment\scripts\application.yaml
        $ctx.AutoTrack("ArgoCDApplication", "nagp-applications")
    } else {
        Log-Info "ArgoCD applications already deployed"
    }
})

Log-Success "Cluster deployment completed successfully"
Show-CompletionSummary

} catch {
    Log-Error "Deployment failed: $_"
    
    Write-Host ""
    Write-Host "═════════════════════════════════════════════════════════════" -ForegroundColor Red
    Write-Host "  DEPLOYMENT ERROR" -ForegroundColor Red
    Write-Host "═════════════════════════════════════════════════════════════" -ForegroundColor Red
    Write-Host ""
    Write-Host "Error Details:" -ForegroundColor Red
    Write-Host $_ -ForegroundColor Yellow
    Write-Host ""
    
    # Show tracked resources for cleanup
    $trackedResources = $ctx.GetTrackedForCleanup()
    if ($trackedResources.Count -gt 0) {
        Write-Host "Resources created before failure (for potential cleanup):" -ForegroundColor Yellow
        $trackedResources.GetEnumerator() | ForEach-Object {
            Write-Host "  - [$($_.Value.Type)] $($_.Value.Name)" -ForegroundColor Gray
        }
        Write-Host ""
        
        $cleanupChoice = Read-Host "Would you like to clean up the created resources? (yes/no)"
        if ($cleanupChoice -eq "yes") {
            Log-Warning "Starting cleanup of tracked resources"
            Invoke-ResourceCleanup
            Log-Warning "Cleanup completed"
        }
    }
    
    Write-Host "Log file: $(Get-LogPath)" -ForegroundColor Cyan
    Write-Host ""
    
    exit 1
}

# ============================================================================
# Completion
# ============================================================================
Log-Info "═════════════════════════════════════════════════════════════"
Log-Info "Kubernetes Cluster Setup Completed Successfully"
Log-Info "═════════════════════════════════════════════════════════════"
Display-LogSummary
Show-LogLocation
