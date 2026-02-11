# Setup Wizard Module using Spectre.Console
# Provides interactive UI for cluster configuration

# Install and import Spectre.Console module
$script:SpectreAvailable = $false

function Get-LatestNuGetPackageVersion {
    param(
        [Parameter(Mandatory)]
        [string]$PackageId
    )

    $packageLower = $PackageId.ToLower()
    $indexUrl = "https://api.nuget.org/v3-flatcontainer/$packageLower/index.json"
    $response = Invoke-RestMethod -Uri $indexUrl -Method Get -ErrorAction Stop
    return $response.versions[-1]
}

function Load-NuGetPackageAssemblies {
    param(
        [Parameter(Mandatory)]
        [string]$PackageId,
        [string]$Version
    )

    if ([string]::IsNullOrWhiteSpace($Version)) {
        $Version = Get-LatestNuGetPackageVersion -PackageId $PackageId
    }

    $cacheRoot = Join-Path $env:LOCALAPPDATA "Spectre.Console"
    if (-not (Test-Path $cacheRoot)) {
        New-Item -ItemType Directory -Path $cacheRoot | Out-Null
    }

    $packageLower = $PackageId.ToLower()
    $nupkgPath = Join-Path $cacheRoot "$PackageId.$Version.nupkg"
    if (-not (Test-Path $nupkgPath)) {
        $nupkgUrl = "https://api.nuget.org/v3-flatcontainer/$packageLower/$Version/$packageLower.$Version.nupkg"
        Invoke-WebRequest -Uri $nupkgUrl -OutFile $nupkgPath -UseBasicParsing
    }

    $extractRoot = Join-Path $cacheRoot "$PackageId.$Version"
    if (-not (Test-Path $extractRoot)) {
        Expand-Archive -Path $nupkgPath -DestinationPath $extractRoot -Force
    }

    $tfmCandidates = @("net8.0", "net7.0", "net6.0", "netstandard2.0")
    $dllDir = $null
    foreach ($tfm in $tfmCandidates) {
        $candidate = Join-Path $extractRoot ("lib\\$tfm")
        if (Test-Path $candidate) {
            $dllDir = $candidate
            break
        }
    }

    if (-not $dllDir) {
        $dllDir = Join-Path $extractRoot "lib"
    }

    $assemblies = Get-ChildItem -Path $dllDir -Filter "*.dll" -Recurse | Sort-Object -Property Name
    foreach ($assembly in $assemblies) {
        try {
            Add-Type -Path $assembly.FullName -ErrorAction Stop
        } catch {
            # Ignore already loaded or incompatible assemblies
        }
    }
}

function Load-SpectreConsoleFromNuGet {
    param(
        [string]$Version
    )

    Load-NuGetPackageAssemblies -PackageId "Spectre.Console.Ansi"
    Load-NuGetPackageAssemblies -PackageId "Spectre.Console" -Version $Version
}

function Initialize-SpectreConsole {
    try {
        if (Get-Module -ListAvailable -Name Spectre.Console) {
            Import-Module Spectre.Console -ErrorAction Stop
        } else {
            Load-SpectreConsoleFromNuGet
        }

        $script:SpectreAvailable = $true
    } catch {
        $script:SpectreAvailable = $false
        throw "Spectre.Console is required for TUI mode. Install the module or allow NuGet download, then re-run the script."
    }
}

if (-not (Get-Command Write-SpectreHost -ErrorAction SilentlyContinue)) {
    function Write-SpectreHost {
        param(
            [Parameter(Position = 0, ValueFromPipeline = $true)]
            $Message
        )

        if ($null -eq $Message) {
            [Spectre.Console.AnsiConsole]::WriteLine("")
            return
        }

        $renderableType = [type]::GetType("Spectre.Console.IRenderable, Spectre.Console", $false)
        if ($renderableType -and $renderableType.IsInstanceOfType($Message)) {
            [Spectre.Console.AnsiConsole]::Write($Message)
            return
        }

        try {
            [Spectre.Console.AnsiConsole]::MarkupLine($Message.ToString())
        } catch {
            $clean = $Message -replace '\[[^\]]+\]', ''
            Write-Host $clean
        }
    }
}

# Helper to escape text for Spectre markup (prevents brackets in dynamic text from being parsed)
function Escape-SpectreMarkup {
    param([string]$Text)
    return ($Text -replace '\[', '[[') -replace '\]', ']]'
}

# Helper to render a Spectre Rule (horizontal separator with title)
function Write-SpectreRule {
    param([string]$Title, [string]$Color = "cyan")
    try {
        $rule = [Spectre.Console.Rule]::new("[$Color]$Title[/]")
        [Spectre.Console.AnsiConsole]::Write($rule)
        [Spectre.Console.AnsiConsole]::WriteLine("")
    } catch {
        Write-Host "--- $Title ---"
    }
}

if (-not (Get-Command Read-SpectreText -ErrorAction SilentlyContinue)) {
    function Read-SpectreText {
        param(
            [string]$Prompt,
            [switch]$Secret,
            [switch]$AllowEmpty
        )

        try {
            $textPrompt = [Spectre.Console.TextPrompt[string]]::new($Prompt)
            if ($Secret) {
                if ($textPrompt.PSObject.Methods.Name -contains "Secret") {
                    $textPrompt = $textPrompt.Secret()
                } elseif ($textPrompt.PSObject.Properties.Name -contains "IsSecret") {
                    $textPrompt.IsSecret = $true
                }
            }
            if ($AllowEmpty) {
                if ($textPrompt.PSObject.Methods.Name -contains "AllowEmpty") {
                    $textPrompt = $textPrompt.AllowEmpty()
                } elseif ($textPrompt.PSObject.Properties.Name -contains "AllowEmpty") {
                    $textPrompt.AllowEmpty = $true
                }
            }
            return [Spectre.Console.AnsiConsole]::Prompt($textPrompt)
        } catch {
            # Fallback for non-interactive terminals
            if ($Secret) {
                return Read-Host -Prompt $Prompt -AsSecureString | ConvertFrom-SecureString -AsPlainText
            } else {
                return Read-Host -Prompt $Prompt
            }
        }
    }
}

if (-not (Get-Command Read-SpectreSelection -ErrorAction SilentlyContinue)) {
    function Read-SpectreSelection {
        param(
            [Parameter(Mandatory)]
            [string[]]$Choices,
            [string]$Prompt = "Select an option"
        )

        try {
            $selection = [Spectre.Console.SelectionPrompt[string]]::new()
            $selection.Title = $Prompt
            foreach ($choice in $Choices) {
                $selection.AddChoice($choice) | Out-Null
            }
            return [Spectre.Console.AnsiConsole]::Prompt($selection)
        } catch {
            # Fallback: numbered list for non-ANSI terminals
            Write-Host $Prompt
            for ($i = 0; $i -lt $Choices.Count; $i++) {
                Write-Host "  [$($i + 1)] $($Choices[$i])"
            }
            do {
                $input = Read-Host "Enter number (1-$($Choices.Count))"
                $num = 0
                $valid = [int]::TryParse($input, [ref]$num) -and $num -ge 1 -and $num -le $Choices.Count
            } while (-not $valid)
            return $Choices[$num - 1]
        }
    }
}

if (-not (Get-Command Confirm-Spectre -ErrorAction SilentlyContinue)) {
    function Confirm-Spectre {
        param(
            [Parameter(Mandatory)]
            [string]$Question,
            [bool]$DefaultAnswer = $true
        )

        try {
            $confirm = [Spectre.Console.ConfirmationPrompt]::new($Question)
            $confirm.DefaultValue = $DefaultAnswer
            return [Spectre.Console.AnsiConsole]::Prompt($confirm)
        } catch {
            # Fallback for non-ANSI terminals
            $default = if ($DefaultAnswer) { "y" } else { "n" }
            $response = Read-Host "$Question (y/n) [$default]"
            if ([string]::IsNullOrWhiteSpace($response)) { return $DefaultAnswer }
            return $response -eq "y" -or $response -eq "Y"
        }
    }
}

if (-not (Get-Command New-SpectreTable -ErrorAction SilentlyContinue)) {
    function New-SpectreTable {
        param(
            [string]$Title
        )

        $table = [Spectre.Console.Table]::new()
        if ($Title) {
            $tableTitleType = [type]::GetType("Spectre.Console.TableTitle, Spectre.Console", $false)
            if ($tableTitleType) {
                $table.Title = [Spectre.Console.TableTitle]::new($Title)
            } else {
                try {
                    $table.Title = $Title
                } catch {
                    # Ignore title if unsupported
                }
            }
        }
        return $table
    }
}

if (-not (Get-Command Add-SpectreTableColumn -ErrorAction SilentlyContinue)) {
    function Add-SpectreTableColumn {
        param(
            [Parameter(Mandatory, ValueFromPipeline = $true)]
            [Spectre.Console.Table]$Table,
            [Parameter(Mandatory)]
            [string]$Header,
            [int]$Width
        )

        $column = [Spectre.Console.TableColumn]::new($Header)
        if ($Width -gt 0) { $column.Width = $Width }
        $Table.AddColumn($column) | Out-Null
        return $Table
    }
}

if (-not (Get-Command Add-SpectreTableRow -ErrorAction SilentlyContinue)) {
    function Add-SpectreTableRow {
        param(
            [Parameter(Mandatory, ValueFromPipeline = $true)]
            [Spectre.Console.Table]$Table,
            [Parameter(Mandatory)]
            [string[]]$Cells
        )

        $Table.AddRow($Cells) | Out-Null
        return $Table
    }
}

# Display wizard header
function Show-WizardHeader {
    Write-SpectreRule "Kubernetes Cluster Setup Wizard" "cyan"
    Write-SpectreHost ""
}

# Prompt for ArgoCD password
function Get-ArgoCDPassword {
    Write-SpectreHost "[yellow]ArgoCD Configuration[/]"
    $password = Read-SpectreText -Prompt "Enter ArgoCD admin password" -Secret -AllowEmpty
    if ([string]::IsNullOrWhiteSpace($password)) {
        $password = "admin"
        Write-SpectreHost "[grey](using default: admin)[/]"
    }
    Write-SpectreHost ""
    return $password
}

# Prompt for Grafana password
function Get-GrafanaPassword {
    Write-SpectreHost "[yellow]Grafana Configuration[/]"
    $password = Read-SpectreText -Prompt "Enter Grafana admin password" -Secret -AllowEmpty
    if ([string]::IsNullOrWhiteSpace($password)) {
        $password = "changeme"
        Write-SpectreHost "[grey](using default: changeme)[/]"
    }
    Write-SpectreHost ""
    return $password
}

# Prompt for New Relic API Key
function Get-NewRelicApiKey {
    Write-SpectreHost "[yellow]New Relic Configuration[/]"
    $envApiKey = $env:NEW_RELIC_API_KEY
    if (-not [string]::IsNullOrWhiteSpace($envApiKey)) {
        Write-SpectreHost "[grey](using NEW_RELIC_API_KEY from environment)[/]"
        Write-SpectreHost ""
        return $envApiKey
    }

    $apiKey = Read-SpectreText -Prompt "Enter New Relic API key (optional, press Enter to skip)" -Secret -AllowEmpty
    if ([string]::IsNullOrWhiteSpace($apiKey)) {
        Write-SpectreHost "[grey](New Relic integration skipped)[/]"
        return $null
    }
    Write-SpectreHost ""
    return $apiKey
}

# Display configuration summary
function Show-ConfigurationSummary {
    param(
        [string]$ArgoCDPassword,
        [string]$GrafanaPassword,
        [string]$NewRelicApiKey
    )

    Write-SpectreRule "Configuration Summary" "green"

    Write-SpectreHost "[green]ArgoCD[/]: Password configured"
    Write-SpectreHost "[green]Grafana[/]: Password configured"
    if ([string]::IsNullOrWhiteSpace($NewRelicApiKey)) {
        Write-SpectreHost "[yellow]New Relic[/]: SKIP"
    } else {
        Write-SpectreHost "[green]New Relic[/]: API Key provided"
    }

    Write-SpectreHost ""
}

# Confirm before proceeding
function Confirm-Setup {
    $proceed = Confirm-Spectre -Question "Proceed with cluster setup?" -DefaultAnswer $true
    return $proceed
}

# Display start message
function Show-StartMessage {
    Write-SpectreHost "[cyan]Starting cluster setup...[/]"
    Write-SpectreHost ""
}

# Display section header with Spectre Rule
function Show-SectionHeader {
    param(
        [string]$Title
    )
    # Strip any existing Spectre color tags for the rule title
    $cleanTitle = $Title -replace '\[/?[a-z]+\]', ''
    Write-SpectreRule $cleanTitle "cyan"
}

# Prompt for Kubernetes cluster type (local or remote)
function Get-KubernetesClusterType {
    Write-SpectreHost "[yellow]Kubernetes Cluster Selection[/]"
    
    $choice = Read-SpectreSelection -Choices @("Create Local Cluster (k3d)", "Use Remote Cluster") -Prompt "Select Kubernetes cluster"
    
    if ($choice -eq "Create Local Cluster (k3d)") {
        return "local"
    } else {
        return "remote"
    }
}

# Get list of available Kubernetes contexts
function Get-KubernetesContexts {
    try {
        $contexts = kubectl config get-contexts -o name 2>$null
        return @($contexts)
    } catch {
        return @()
    }
}

# Prompt user to select a Kubernetes context
function Select-KubernetesContext {
    $contexts = Get-KubernetesContexts
    
    if ($contexts.Count -eq 0) {
        Write-SpectreHost "[red]No Kubernetes contexts found. Please configure kubectl first.[/]"
        return $null
    }

    Write-SpectreHost "[yellow]Available Kubernetes Contexts[/]"
    $selectedContext = Read-SpectreSelection -Choices $contexts -Prompt "Select context"
    return $selectedContext
}

# Set Kubernetes context
function Set-KubernetesContext {
    param(
        [string]$Context
    )
    
    try {
        kubectl config use-context $Context | Out-Null
        Show-Success "Switched to context: $Context"
        return $true
    } catch {
        Write-SpectreHost "[red]ERR[/] Failed to switch context: $_"
        return $false
    }
}

# Confirm Kubernetes cluster selection
function Confirm-ClusterSelection {
    param(
        [string]$ClusterType,
        [string]$Context
    )
    
    Write-SpectreHost ""
    Write-SpectreRule "Kubernetes Cluster Configuration" "green"
    
    if ($ClusterType -eq "local") {
        Write-SpectreHost "[cyan]Cluster Type: [/][yellow]Local (k3d)[/]"
    } else {
        Write-SpectreHost "[cyan]Cluster Type: [/][yellow]Remote[/]"
        Write-SpectreHost "[cyan]Context: [/][yellow]$Context[/]"
    }
    
    Write-SpectreHost ""
    $proceed = Confirm-Spectre -Question "Proceed with this cluster configuration?" -DefaultAnswer $true
    return $proceed
}

# Get Kubernetes platform for Istio installation
function Get-KubernetesPlatform {
    Write-SpectreHost "[yellow]Kubernetes Platform Selection[/]"
    
    $platforms = @(
        "AWS EKS",
        "Azure AKS",
        "Google GKE",
        "Oracle OCI",
        "Docker Desktop",
        "kind",
        "Minikube",
        "MicroK8s",
        "OpenShift",
        "Generic Kubernetes"
    )
    
    $selectedPlatform = Read-SpectreSelection -Choices $platforms -Prompt "Select your Kubernetes platform"
    return $selectedPlatform
}

# Map platform name to Istio platform value
function Get-IstioPlatformValue {
    param(
        [string]$Platform
    )
    
    $platformMap = @{
        "AWS EKS" = "aws"
        "Azure AKS" = "azure"
        "Google GKE" = "gke"
        "Oracle OCI" = "oci"
        "Docker Desktop" = "docker"
        "kind" = "kind"
        "Minikube" = "minikube"
        "MicroK8s" = "microk8s"
        "OpenShift" = "openshift"
        "Generic Kubernetes" = "generic"
    }
    
    return $platformMap[$Platform]
}

# Display section header with status
function Show-SectionHeader {
    param(
        [string]$Title
    )
    $cleanTitle = $Title -replace '\[/?[a-z]+\]', ''
    Write-SpectreRule $cleanTitle "cyan"
}

# Display success message
function Show-Success {
    param(
        [string]$Message
    )
    $safeMsg = Escape-SpectreMarkup $Message
    Write-SpectreHost "[green]OK[/] $safeMsg"
}

# Display info message
function Show-Info {
    param(
        [string]$Message
    )
    $safeMsg = Escape-SpectreMarkup $Message
    Write-SpectreHost "[grey]SKIP[/] $safeMsg"
}

# Display warning message
function Show-Warning {
    param(
        [string]$Message
    )
    $safeMsg = Escape-SpectreMarkup $Message
    Write-SpectreHost "[yellow]WRN[/] $safeMsg"
}

# Display cancellation message
function Show-CancelledMessage {
    Write-SpectreHost "[yellow]Setup cancelled.[/]"
}

# Display completion summary
function Show-CompletionSummary {
    Write-SpectreHost ""
    Write-SpectreRule "Setup Completed Successfully!" "green"
    Write-SpectreHost ""
    Write-SpectreHost "[cyan]Your Kubernetes Cluster is Ready:[/]"
    Write-SpectreHost "  [yellow]ArgoCD UI:[/]    kubectl port-forward service/argocd-server -n argocd 8080:443"
    Write-SpectreHost "  [yellow]Grafana:[/]      kubectl port-forward service/grafana -n observability 3000:80"
    Write-SpectreHost "  [yellow]Prometheus:[/]   kubectl port-forward service/prometheus -n observability 9090:9090"
    Write-SpectreHost ""
    Write-SpectreHost "[green]All components installed and configured[/]"
    Write-SpectreHost ""
}

# Display prerequisite check results
Export-ModuleMember -Function @(
    'Initialize-SpectreConsole',
    'Show-WizardHeader',
    'Get-ArgoCDPassword',
    'Get-GrafanaPassword',
    'Get-NewRelicApiKey',
    'Show-ConfigurationSummary',
    'Confirm-Setup',
    'Show-StartMessage',
    'Show-SectionHeader',
    'Show-Success',
    'Show-Info',
    'Show-Warning',
    'Show-CancelledMessage',
    'Show-CompletionSummary',
    'Get-KubernetesClusterType',
    'Get-KubernetesContexts',
    'Select-KubernetesContext',
    'Set-KubernetesContext',
    'Confirm-ClusterSelection',
    'Get-KubernetesPlatform',
    'Get-IstioPlatformValue'
)

