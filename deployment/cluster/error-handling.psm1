# Error Handling Module
# Provides command validation and execution wrappers

# ============================================================================
# Command Validation Functions
# ============================================================================

# Test if a command is available in the system
function Test-CommandExists {
    param([string]$CommandName)
    
    $command = Get-Command $CommandName -ErrorAction SilentlyContinue
    return $null -ne $command
}

# ============================================================================
# Command Execution Wrappers
# ============================================================================

# Track created resources for cleanup
$script:TrackedResources = @()

# Invoke command with retry logic for transient failures
function Invoke-CommandWithRetry {
    param(
        [scriptblock]$ScriptBlock,
        [int]$MaxRetries = 3,
        [int]$DelaySeconds = 2,
        [string]$StepName = "Operation"
    )
    
    $attempt = 0
    $lastError = $null
    
    while ($attempt -lt $MaxRetries) {
        try {
            $attempt++
            Write-Verbose "Attempt $attempt of $MaxRetries for: $StepName"
            
            & $ScriptBlock
            return $true
        } catch {
            $lastError = $_
            
            if ($attempt -lt $MaxRetries) {
                $waitTime = $DelaySeconds * $attempt  # Exponential backoff
                Write-Verbose "Attempt $attempt failed. Waiting $waitTime seconds before retry..."
                Start-Sleep -Seconds $waitTime
            }
        }
    }
    
    throw "Failed after $MaxRetries attempts. Last error: $($lastError.Exception.Message)"
}

# Track a resource for cleanup
function Register-CreatedResource {
    param(
        [string]$ResourceType,
        [string]$ResourceName,
        [string]$Namespace = "default"
    )
    
    $resource = @{
        Type = $ResourceType
        Name = $ResourceName
        Namespace = $Namespace
        CreatedAt = Get-Date
    }
    
    $script:TrackedResources += $resource
}

# Get all tracked resources
function Get-TrackedResources {
    return $script:TrackedResources
}

# Clear tracked resources
function Clear-TrackedResources {
    $script:TrackedResources = @()
}

# Cleanup created resources in reverse order
function Invoke-ResourceCleanup {
    param(
        [switch]$Confirm
    )
    
    if ($script:TrackedResources.Count -eq 0) {
        Write-Host "No resources to clean up."
        return
    }
    
    Write-Host ""
    Write-Host "Resources created during setup:"
    $script:TrackedResources | ForEach-Object {
        Write-Host "  - $($_.Type): $($_.Name) (Namespace: $($_.Namespace))"
    }
    
    if ($Confirm) {
        $userConfirm = Read-Host "Delete all created resources? (y/n)"
        if ($userConfirm -ne "y" -and $userConfirm -ne "Y") {
            Write-Host "Cleanup cancelled."
            return
        }
    }
    
    # Delete in reverse order (last created first)
    [array]::Reverse($script:TrackedResources)
    
    foreach ($resource in $script:TrackedResources) {
        try {
            switch ($resource.Type) {
                "Namespace" {
                    Write-Host "Deleting namespace: $($resource.Name)"
                    kubectl delete namespace $resource.Name --ignore-not-found | Out-Null
                }
                "Secret" {
                    Write-Host "Deleting secret: $($resource.Name) in $($resource.Namespace)"
                    kubectl delete secret $resource.Name -n $resource.Namespace --ignore-not-found | Out-Null
                }
                "HelmRelease" {
                    Write-Host "Uninstalling helm release: $($resource.Name) in $($resource.Namespace)"
                    helm uninstall $resource.Name -n $resource.Namespace --ignore-not-found | Out-Null
                }
                "K3dCluster" {
                    Write-Host "Deleting k3d cluster: $($resource.Name)"
                    k3d cluster delete $resource.Name | Out-Null
                }
                default {
                    Write-Host "Skipping cleanup for $($resource.Type): $($resource.Name) (no cleanup handler)"
                }
            }
        } catch {
            Write-Warning "Failed to delete $($resource.Type) $($resource.Name): $_"
        }
    }
    
    Write-Host "Cleanup completed."
}

# ============================================================================
# Deployment Context Infrastructure for Cross-Cutting Concerns
# ============================================================================

class DeploymentContext {
    [string]$CurrentOperation
    [string]$CurrentPhase
    [hashtable]$TrackedResources = @{}
    [bool]$HasError = $false
    [string]$LastError = ""
    [int]$OperationCount = 0
    
    [void] ExecuteStep([string]$Description, [scriptblock]$CommandBlock) {
        $this.CurrentOperation = $Description
        $this.OperationCount++
        
        Log-StepStart $Description
        try {
            & $CommandBlock
            Log-StepComplete $Description
        } catch {
            $this.HasError = $true
            $this.LastError = $_
            Log-StepFailed "$Description - $_"
            throw
        }
    }
    
    [void] AutoTrack([string]$ExpectedType, [string]$ResourceName) {
        $this.AutoTrack($ExpectedType, $ResourceName, "")
    }
    
    [void] AutoTrack([string]$ExpectedType, [string]$ResourceName, [string]$Namespace) {
        if (-not $Namespace) { $Namespace = "" }
        $key = if ($Namespace) { "$ExpectedType/$Namespace/$ResourceName" } else { "$ExpectedType/$ResourceName" }
        
        if (-not $this.TrackedResources.ContainsKey($key)) {
            $this.TrackedResources[$key] = @{
                Type = $ExpectedType
                Name = $ResourceName
                Namespace = $Namespace
                CreatedAt = Get-Date
            }
            Register-CreatedResource -ResourceType $ExpectedType -ResourceName $ResourceName -Namespace $Namespace
        }
    }
    
    [int] GetTrackedCount() {
        return $this.TrackedResources.Count
    }
    
    [hashtable] GetTrackedForCleanup() {
        return $this.TrackedResources
    }
}

# Initialize global deployment context
$script:deploymentContext = [DeploymentContext]::new()

function Get-DeploymentContext {
    return $script:deploymentContext
}

Export-ModuleMember -Function @(
    'Test-CommandExists',
    'Invoke-CommandWithRetry',
    'Invoke-ResourceCleanup',
    'Get-DeploymentContext'
)
