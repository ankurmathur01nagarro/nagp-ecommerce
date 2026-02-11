# Idempotency Module
# Provides checks for resource existence to enable safe script re-runs

# ============================================================================
# Namespace Checks
# ============================================================================

# Test if a Kubernetes namespace exists
function Test-NamespaceExists {
    param([string]$Namespace)
    
    try {
        $result = kubectl get namespace $Namespace -o json 2>$null
        return $null -ne $result
    } catch {
        return $false
    }
}

# ============================================================================
# Secret Checks
# ============================================================================

# Test if a secret exists in a namespace
function Test-SecretExists {
    param(
        [string]$SecretName,
        [string]$Namespace = "default"
    )
    
    try {
        $result = kubectl get secret $SecretName -n $Namespace -o json 2>$null
        return $null -ne $result
    } catch {
        return $false
    }
}

# ============================================================================
# Helm Release Checks
# ============================================================================

# Test if a helm release is installed
function Test-HelmReleaseExists {
    param(
        [string]$ReleaseName,
        [string]$Namespace = "default"
    )
    
    try {
        $result = helm list -n $Namespace -o json 2>$null | ConvertFrom-Json
        $release = $result | Where-Object { $_.name -eq $ReleaseName }
        return $null -ne $release
    } catch {
        return $false
    }
}

# ============================================================================
# Cluster Checks
# ============================================================================

# Test if k3d cluster exists
function Test-K3dClusterExists {
    param([string]$ClusterName = "local")
    
    try {
        $result = k3d cluster list 2>$null | Select-String $ClusterName
        return $null -ne $result
    } catch {
        return $false
    }
}

# Get k3d cluster info
function Get-K3dClusterStatus {
    param([string]$ClusterName = "local")
    
    try {
        $result = k3d cluster list -o json 2>$null | ConvertFrom-Json
        $cluster = $result | Where-Object { $_.name -eq $ClusterName }
        return $cluster
    } catch {
        return $null
    }
}

# ============================================================================
# Service Mesh Checks
# ============================================================================

# Test if Istio is installed
function Test-IstioInstalled {
    try {
        $result = kubectl get namespace istio-system -o json 2>$null
        if ($null -eq $result) { return $false }
        
        $pods = kubectl get pods -n istio-system -o json 2>$null | ConvertFrom-Json
        return $pods.items.Count -gt 0
    } catch {
        return $false
    }
}

# Wait for Istio pods to be ready
function Wait-IstioPodsReady {
    param([int]$TimeoutSeconds = 300)
    
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    while ($stopwatch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        try {
            $result = kubectl get pods -n istio-system -o json 2>$null | ConvertFrom-Json
            $allReady = $true
            
            foreach ($pod in $result.items) {
                $ready = $pod.status.containerStatuses.ready | Where-Object { $_ -eq $true } | Measure-Object | Select-Object -ExpandProperty Count
                if ($ready -ne $pod.status.containerStatuses.Count) {
                    $allReady = $false
                    break
                }
            }
            
            if ($allReady) {
                return $true
            }
        } catch {
            # Continue waiting
        }
        
        Start-Sleep -Seconds 5
    }
    
    return $false
}

# ============================================================================
# ArgoCD Checks
# ============================================================================

# Test if ArgoCD is installed
function Test-ArgoCDInstalled {
    try {
        $result = kubectl get namespace argocd -o json 2>$null
        if ($null -eq $result) { return $false }
        
        $release = helm list -n argocd -o json 2>$null | ConvertFrom-Json | Where-Object { $_.name -eq "argocd" }
        return $null -ne $release
    } catch {
        return $false
    }
}

# Wait for ArgoCD pods to be ready
function Wait-ArgoCDPodsReady {
    param([int]$TimeoutSeconds = 300)
    
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    while ($stopwatch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        try {
            $result = kubectl get pods -n argocd -o json 2>$null | ConvertFrom-Json
            if ($result.items.Count -eq 0) {
                Start-Sleep -Seconds 5
                continue
            }
            
            $allReady = $true
            foreach ($pod in $result.items) {
                if ($pod.status.phase -ne "Running") {
                    $allReady = $false
                    break
                }
            }
            
            if ($allReady) {
                return $true
            }
        } catch {
            # Continue waiting
        }
        
        Start-Sleep -Seconds 5
    }
    
    return $false
}

# ============================================================================
# Custom Resource Checks
# ============================================================================

# Test if Gateway API CRDs are installed
function Test-GatewayAPICRDsInstalled {
    try {
        $result = kubectl get crd gateways.gateway.networking.k8s.io -o json 2>$null
        return $null -ne $result
    } catch {
        return $false
    }
}

# Test if ArgoCD Application resources are created
function Test-ArgoCDApplicationsExist {
    try {
        $result = kubectl get applications -n argocd -o json 2>$null | ConvertFrom-Json
        return $result.items.Count -gt 0
    } catch {
        return $false
    }
}

# ============================================================================
# General State Checks
# ============================================================================

# Get overall cluster setup status
function Get-ClusterSetupStatus {
    return @{
        K3dCluster = Test-K3dClusterExists "local"
        IstioInstalled = Test-IstioInstalled
        ArgoCDInstalled = Test-ArgoCDInstalled
        GatewayAPICRDs = Test-GatewayAPICRDsInstalled
        ArgoCDApplications = Test-ArgoCDApplicationsExist
        NamespaceObservability = Test-NamespaceExists "observability"
        NamespaceArgoCD = Test-NamespaceExists "argocd"
        NamespaceNagpEcom = Test-NamespaceExists "nagp-ecom"
    }
}

# Display cluster status
function Show-ClusterSetupStatus {
    $status = Get-ClusterSetupStatus

    Write-Host ""
    Write-Host "==============================================================="
    Write-Host "                    Cluster Status Check"
    Write-Host "==============================================================="

    foreach ($item in $status.GetEnumerator()) {
        $icon = if ($item.Value) { "[YES]" } else { "[NO]" }
        Write-Host "$icon $($item.Key)"
    }

    Write-Host ""
}

Export-ModuleMember -Function @(
    'Test-NamespaceExists',
    'Test-SecretExists',
    'Test-HelmReleaseExists',
    'Test-K3dClusterExists',
    'Get-K3dClusterStatus',
    'Test-IstioInstalled',
    'Wait-IstioPodsReady',
    'Test-ArgoCDInstalled',
    'Wait-ArgoCDPodsReady',
    'Test-GatewayAPICRDsInstalled',
    'Test-ArgoCDApplicationsExist',
    'Get-ClusterSetupStatus',
    'Show-ClusterSetupStatus'
)

