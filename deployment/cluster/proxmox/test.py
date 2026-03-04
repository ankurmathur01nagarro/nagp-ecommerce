from pathlib import Path

from plumbum import local, BG, FG
from rich.console import Console
import proxmox_helper as ph
c = Console()


kubeconfig_env = local.env(KUBECONFIG=f"{Path('./').absolute().joinpath('kubeconfig')}")
with kubeconfig_env:
    c.rule(f"[bold blue]Tainting control plane node to prevent scheduling regular workloads[/]")
    _ = local["kubectl"]["taint", "nodes", "k3s-control", "node-role.kubernetes.io/control-plane=:NoSchedule", "--overwrite"] & FG
    
    c.rule(f"[bold blue]Setting up Democratic CSI[/]")

    # Patch the committed template files with runtime values and write the gitignored output files.
    # Templates (config-*.yaml.tpl) define the config structure — Python only fills in the placeholders.
    # Kustomize secretGenerator reads the output files and wraps them into K8s Secrets.
    overlay_path = Path("..") / "scripts" / "democratic-csi" / "overlays" / "local-truenas"

    substitutions = {
        "TRUENAS_IP":          "192.168.1.18",
        "TRUENAS_API_KEY":     "1-H1Bv0ZdX77XVQeAueICdTHPLfmj0cyUMqch2VV4VO9yWCLrlOZhYRnSihtWAeTJ2",
        "NFS_DATASET_PARENT":  "pool1/k8s-nfs",
        "ISCSI_DATASET_PARENT": "pool1/pool1-iscsi",
        "TRUENAS_NETWORK_CIDR": "192.168.1.0/24",
    }

    for tpl_name, out_name in [("config-nfs.yaml.tpl", "config-nfs.yaml"),
                                ("config-iscsi.yaml.tpl", "config-iscsi.yaml")]:
        tpl_text = (overlay_path / tpl_name).read_text()
        patched = tpl_text.format_map(substitutions)
        (overlay_path / out_name).write_text(patched)
        c.log(f"[green]Patched {tpl_name} → {out_name}[/]")

    csi_cmd = local["pwsh"]["-Command", """&{
        kubectl kustomize ..\\scripts\\democratic-csi\\overlays\\local-truenas --enable-helm | kubectl apply -f -
    }"""]
    future = csi_cmd & BG
    while not future.poll():
        if future.stdout is not None:
            for line in future.stdout:
                c.print(line, end="", style="grey50 dim")
    future.wait()
    c.rule(f"[bold green]✅ Democratic CSI Installed")

c.log(f"[yellow bold]⚠️Your cluster is ready! Test it with: kubectl get nodes --kubeconfig {Path('./').absolute().joinpath('kubeconfig')}[/]")