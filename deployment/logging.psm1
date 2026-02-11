# Logging Module
# Provides centralized logging to both console and file with Spectre.Console formatting

# Global logging variables
$script:LogPath = $null
$script:LogStream = $null
$script:LogStartTime = Get-Date

# Unified logging write function
function Write-SpectreHostSafe {
    param(
        [Parameter(Position = 0)]
        [string]$Message,
        
        [switch]$NoNewline
    )
    
    $ansiConsoleType = [type]::GetType("Spectre.Console.AnsiConsole, Spectre.Console", $false)

    if ($ansiConsoleType) {
        try {
            if ($NoNewline) {
                $ansiConsoleType::Markup($Message.ToString())
            } else {
                $ansiConsoleType::MarkupLine($Message.ToString())
            }
        } catch {
            $cleanMessage = $Message -replace '\[[^\]]+\]', ''
            Write-Host $cleanMessage -NoNewline:$NoNewline
        }
    } else {
        $cleanMessage = $Message -replace '\[[^\]]+\]', ''
        Write-Host $cleanMessage -NoNewline:$NoNewline
    }
}

# Initialize logging to file
function Initialize-Logging {
    param(
        [string]$LogDirectory = "."
    )
    
    $timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
    $script:LogPath = Join-Path $LogDirectory "cluster-setup-$timestamp.log"
    
    try {
        # Create log file with header
        $header = @"
================================================================================
Kubernetes Cluster Setup Wizard - Execution Log
Started: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
PowerShell Version: $($PSVersionTable.PSVersion)
OS: $([System.Runtime.InteropServices.RuntimeInformation]::OSDescription)
================================================================================
"@
        Set-Content -Path $script:LogPath -Value $header -Encoding UTF8
        return $true
    } catch {
        Write-Error "Failed to initialize logging: $_"
        return $false
    }
}

# Write message to both console and log file
function Log-Message {
    param(
        [string]$Message,
        [ValidateSet("INFO", "SUCCESS", "WARNING", "ERROR", "DEBUG")]
        [string]$Level = "INFO",
        [bool]$Console = $true
    )
    
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logEntry = "[$timestamp] [$Level] $Message"
    
    # Write to console with formatting
    if ($Console) {
        # Escape any brackets in the message to prevent Spectre markup parsing
        $safeMsg = ($Message -replace '\[', '[[') -replace '\]', ']]'
        switch ($Level) {
            "SUCCESS" {
                Write-SpectreHostSafe "[green]OK[/] $safeMsg"
            }
            "WARNING" {
                Write-SpectreHostSafe "[yellow]WRN[/] $safeMsg"
            }
            "ERROR" {
                Write-SpectreHostSafe "[red]ERR[/] $safeMsg"
            }
            "DEBUG" {
                Write-SpectreHostSafe "[grey]DBG[/] $safeMsg"
            }
            default {
                Write-SpectreHostSafe "[cyan]INF[/] $safeMsg"
            }
        }
    }
    
    # Write to log file
    if ($script:LogPath -and (Test-Path (Split-Path $script:LogPath))) {
        try {
            Add-Content -Path $script:LogPath -Value $logEntry -Encoding UTF8
        } catch {
            Write-Warning "Failed to write to log file: $_"
        }
    }
}

# Log informational message
function Log-Info {
    param([string]$Message)
    Log-Message -Message $Message -Level "INFO"
}

# Log success message
function Log-Success {
    param([string]$Message)
    Log-Message -Message $Message -Level "SUCCESS"
}

# Log warning message
function Log-Warning {
    param([string]$Message)
    Log-Message -Message $Message -Level "WARNING"
}

# Log error message
function Log-Error {
    param([string]$Message)
    Log-Message -Message $Message -Level "ERROR"
}

# Log debug message (only shown in verbose mode)
function Log-Debug {
    param([string]$Message)
    Log-Message -Message $Message -Level "DEBUG"
}

# Log step start
function Log-StepStart {
    param([string]$StepName)
    Log-Message -Message "Starting: $StepName" -Level "INFO"
}

# Log step completion
function Log-StepComplete {
    param(
        [string]$StepName,
        $Duration = $null
    )
    
    if ($Duration -and $Duration -is [timespan]) {
        Log-Success "$StepName completed in $($Duration.TotalSeconds)s"
    } else {
        Log-Success "$StepName completed"
    }
}

# Log step failure
function Log-StepFailed {
    param(
        [string]$StepName,
        [string]$ErrorMessage
    )
    Log-Error "$StepName failed: $ErrorMessage"
}

# Log command execution
function Log-CommandExecution {
    param(
        [string]$Command,
        [string]$Description = ""
    )
    
    $msg = "Executing: $Command"
    if ($Description) {
        $msg = "$Description`n  Command: $Command"
    }
    Log-Debug $msg
}

# Get current log file path
function Get-LogPath {
    return $script:LogPath
}

# Display log file location to user
function Show-LogLocation {
    Write-SpectreHostSafe ""
    Write-SpectreHostSafe "[yellow]Setup Log[/]"
    Write-SpectreHostSafe "Log file location: [cyan]$($script:LogPath)[/]"
    Write-SpectreHostSafe "Use this log for troubleshooting and sharing with support."
}

# Display log file at end
function Display-LogSummary {
    Write-SpectreHostSafe ""
    Write-SpectreHostSafe "[green]Setup Complete[/]"
    Write-SpectreHostSafe ""
    Write-SpectreHostSafe "Full log available at:"
    Write-SpectreHostSafe "[cyan]$($script:LogPath)[/]"
    
    # Show last few lines of log
    if (Test-Path $script:LogPath) {
        Write-SpectreHostSafe ""
        Write-SpectreHostSafe "Recent log entries:"
        Get-Content $script:LogPath -Tail 10 | ForEach-Object {
            $escaped = $_ -replace '\[', '[[' -replace '\]', ']]'
            Write-SpectreHostSafe "[grey]$escaped[/]"
        }
    }
}

Export-ModuleMember -Function @(
    'Initialize-Logging',
    'Log-Message',
    'Log-Info',
    'Log-Success',
    'Log-Warning',
    'Log-Error',
    'Log-Debug',
    'Log-StepStart',
    'Log-StepComplete',
    'Log-StepFailed',
    'Log-CommandExecution',
    'Get-LogPath',
    'Show-LogLocation',
    'Display-LogSummary'
)

