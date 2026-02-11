# Error Handling Module
# Provides command validation and execution wrappers

# ============================================================================
# Command Validation Functions
# ============================================================================

# Test if a command is available in the system
function Test-CommandExists {
    param(
        [string]$CommandName,
        [bool]$IsCritical = $true
    )
    
    $command = Get-Command $CommandName -ErrorAction SilentlyContinue
    return $null -ne $command
}

# ============================================================================
# Command Execution Wrappers
# ============================================================================

# Track created resources for cleanup
$script:TrackedResources = @()

# Invoke a Kubernetes command with error handling
function Invoke-KubernetesCommand {
    param(
        [string]$Command,
        [string]$StepName,
        [bool]$AllowFailure = $false,
        [bool]$TrackResource = $false
    )
    
    try {
        Write-Verbose "Executing: $Command"
        $output = Invoke-Expression $Command -ErrorAction Stop
        return @{
            Success = $true
            Output = $output
            Error = $null
        }
    } catch {
        $errorMsg = $_.Exception.Message
        
        if ($AllowFailure) {
            return @{
                Success = $false
                Output = $null
                Error = $errorMsg
                IsAllowed = $true
            }
        } else {
            throw $_
        }
    }
}

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
function Track-CreatedResource {
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
                "namespace" {
                    Write-Host "Deleting namespace: $($resource.Name)"
                    kubectl delete namespace $resource.Name --ignore-not-found | Out-Null
                }
                "secret" {
                    Write-Host "Deleting secret: $($resource.Name) in $($resource.Namespace)"
                    kubectl delete secret $resource.Name -n $resource.Namespace --ignore-not-found | Out-Null
                }
                "helm" {
                    Write-Host "Uninstalling helm release: $($resource.Name) in $($resource.Namespace)"
                    helm uninstall $resource.Name -n $resource.Namespace --ignore-not-found | Out-Null
                }
                "cluster" {
                    Write-Host "Deleting cluster: $($resource.Name)"
                    k3d cluster delete $resource.Name | Out-Null
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
    
    <#
    .SYNOPSIS
    Execute a step with automatic error handling and logging
    #>
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
    
    <#
    .SYNOPSIS
    Ensure a resource exists or create it, with automatic logging and tracking
    #>
    [void] EnsureResource([string]$Type, [string]$Name, [scriptblock]$CreateBlock, [string]$Namespace = "") {
        $key = if ($Namespace) { "$Type/$Namespace/$Name" } else { "$Type/$Name" }
        
        if ($this.TrackedResources.ContainsKey($key)) {
            Log-Info "$Type '$Name' already tracked"
            return
        }
        
        try {
            & $CreateBlock
            $this.TrackedResources[$key] = @{ 
                Type = $Type
                Name = $Name
                Namespace = $Namespace
                CreatedAt = Get-Date
            }
            Track-CreatedResource -ResourceType $Type -ResourceName $Name -Namespace $Namespace
            Log-Success "$Type '$Name' created"
        } catch {
            Log-Error "Failed to create $Type '$Name': $_"
            throw
        }
    }
    
    <#
    .SYNOPSIS
    Auto-track resources from command output
    #>
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
                Auto = $true
            }
            Track-CreatedResource -ResourceType $ExpectedType -ResourceName $ResourceName -Namespace $Namespace
        }
    }
    
    <#
    .SYNOPSIS
    Conditionally execute a step when test passes (for step skipping)
    #>
    [bool] ExecuteIfMissing([string]$Description, [scriptblock]$TestBlock, [scriptblock]$CommandBlock) {
        if (& $TestBlock) {
            Log-Info "$Description (already exists, skipping)"
            return $false
        }
        
        $this.ExecuteStep($Description, $CommandBlock)
        return $true
    }
    
    <#
    .SYNOPSIS
    Get count of tracked resources
    #>
    [int] GetTrackedCount() {
        return $this.TrackedResources.Count
    }
    
    <#
    .SYNOPSIS
    Export tracked resources for cleanup
    #>
    [hashtable] GetTrackedForCleanup() {
        return $this.TrackedResources
    }
}

# Initialize global deployment context
$script:deploymentContext = [DeploymentContext]::new()

<#
.SYNOPSIS
Get the global deployment context for operation orchestration
#>
function Get-DeploymentContext {
    return $script:deploymentContext
}

<#
.SYNOPSIS
Execute a deployment step with automatic error handling
.DESCRIPTION
Wraps command execution with logging, error tracking, and resource tracking
#>
function Invoke-DeploymentStep {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Description,
        
        [Parameter(Mandatory)]
        [scriptblock]$CommandBlock,
        
        [string]$ResourceType,
        [string]$ResourceName,
        [string]$Namespace
    )
    
    $ctx = Get-DeploymentContext
    Log-StepStart $Description
    
    try {
        & $CommandBlock
        
        if ($ResourceType -and $ResourceName) {
            $ctx.AutoTrack($ResourceType, $ResourceName, $Namespace)
        }
        
        Log-StepComplete $Description
    } catch {
        Log-StepFailed "$Description - $_"
        throw
    }
}

<#
.SYNOPSIS
Ensure a resource exists or create it with automatic tracking
.DESCRIPTION
Tests existence, logs appropriately, creates if missing, tracks creation
#>
function Ensure-Resource {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Type,
        
        [Parameter(Mandatory)]
        [string]$Name,
        
        [Parameter(Mandatory)]
        [scriptblock]$CreateBlock,
        
        [scriptblock]$ExistenceTest,
        
        [string]$Namespace
    )
    
    $ctx = Get-DeploymentContext
    
    # Use provided test or default
    if ($ExistenceTest) {
        $exists = & $ExistenceTest
    } else {
        $exists = $false
    }
    
    if ($exists) {
        Log-Info "$Type '$Name' already exists, skipping creation"
        return
    }
    
    $description = "Create $Type '$Name'"
    if ($Namespace) {
        $description += " in namespace '$Namespace'"
    }
    
    $ctx.ExecuteStep($description, $CreateBlock)
    $ctx.AutoTrack($Type, $Name, $Namespace)
}

Export-ModuleMember -Function @(
    'Test-CommandExists',
    'Invoke-KubernetesCommand',
    'Invoke-CommandWithRetry',
    'Track-CreatedResource',
    'Get-TrackedResources',
    'Clear-TrackedResources',
    'Invoke-ResourceCleanup',
    'Get-DeploymentContext',
    'Invoke-DeploymentStep',
    'Ensure-Resource'
)

