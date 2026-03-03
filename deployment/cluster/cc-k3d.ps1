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
Show-SectionHeader "Installing Tools: k3d, k3sup, kubectl, helm"

$ctx.ExecuteStep("Installing Required Tools", {
    $installedTools = @()
    @("k3d", "kubectl", "helm") | ForEach-Object {
        if (Test-CommandExists -Command $_) {
            Log-Info "$_ already installed"
        } else {
            scoop install $_
        }
        $installedTools += $_
    }
    Show-Success "Tools installed: $($installedTools -join ', ')"
})

try {
    Show-SectionHeader "Creating Local Kubernetes Cluster (k3d)"
    
    $ctx.ExecuteStep("Creating Local Kubernetes Cluster (k3d)", {
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
