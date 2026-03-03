from pathlib import Path

from plumbum import local, BG, FG
from rich.console import Console
import proxmox_helper as ph
c = Console()

kubeconfig_env = local.env(KUBECONFIG=f"{Path('./').absolute().joinpath('kubeconfig')}")
with kubeconfig_env:
    c.rule("[bold blue]Setting up MetalLB[/]")
    metallb_cmd = local["pwsh"]["-Command", """&{
        helm repo add metallb https://metallb.github.io/metallb
        helm repo update
        kubectl create namespace metallb-system
        kubectl label namespace metallb-system `
            pod-security.kubernetes.io/enforce=privileged `
            pod-security.kubernetes.io/audit=privileged `
            pod-security.kubernetes.io/warn=privileged `
            --overwrite
        helm install metallb metallb/metallb --namespace metallb-system -f ..\\scripts\\metallb\\helm-metallb-values.yaml --wait
        kubectl apply -f ..\\scripts\\metallb\\metallb-config.yaml
    }"""]
    future = metallb_cmd & BG
    if future.stdout is not None:
        for line in future.stdout:
            c.print(line, end="", style="dim")
    future.wait()
    c.rule(f"[bold green]✅ MetalLB Installed")

c.log(f"[yellow bold]⚠️Your cluster is ready! Test it with: kubectl get nodes --kubeconfig {Path('./').absolute().joinpath('kubeconfig')}[/]")

