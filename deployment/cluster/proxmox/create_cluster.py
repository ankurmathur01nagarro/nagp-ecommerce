import os
from pathlib import Path
from time import sleep
from plumbum import local, SshMachine, BG, FG, TEE
from proxmoxer import ProxmoxAPI
from rich import print
from rich.prompt import IntPrompt, Prompt
from rich.progress import Progress, SpinnerColumn, TextColumn
from rich.live import Live
from rich.console import Console
from rich.text import Text
import promptly as p
import proxmox_helper as ph
import urllib.parse

c = Console()
py_script_dir = Path(__file__).parent.absolute()
deployment_dir = (py_script_dir / ".." / "..").resolve()
deployment_cluster_dir = (deployment_dir / "cluster").resolve()
deployment_scripts_dir = (deployment_dir / "scripts").resolve()

# Ask for proxmox host ip and credentials to connect with proxmoxer API, or use environment variables for authentication
proxmox_host = Prompt.ask("[green bold]Enter Proxmox host IP address[/]", default="192.168.1.22")
proxmox_port = Prompt.ask("[green bold]Enter Proxmox API port[/]", default="8006")
proxmox_user = Prompt.ask("[green bold]Enter Proxmox username[/]", default="root@pam")
proxmox_password = Prompt.ask("[green bold]Enter Proxmox password[/]", password=True)

prox = ProxmoxAPI(
    host=proxmox_host,
    verify_ssl=False,
    port=int(proxmox_port),
    user=proxmox_user,
    password=proxmox_password,
    # user="ansible@pve",
    # token_name="automation-token",
    # token_value="91619060-b10a-45e3-b9b3-87701b295bd0",
    backend='https',
    service='PVE'
)

# Ask user to select storage where the VM imported disks are stored
print(f"[blue]Select Proxmox storage to import the image (must support 'images' content type)[/]")
vdisk_storage = ph.ask_storage(prox, content_type='images')
# Ask user to select storage where the new VM disks will be created (can be different from image storage)
print(f"[blue]Select Proxmox storage where the new VM disks will be created (must support 'images' content type)[/]")
vm_disk_storage = ph.ask_storage(prox, content_type='images')

# Download Debian 13 cloud-init image and import to Proxmox storage
c.rule(f"[bold blue]Importing image to Proxmox storage[/]")
image_url = "https://cloud.debian.org/images/cloud/trixie/latest/debian-13-genericcloud-amd64.qcow2"
image_url = Prompt.ask("[green bold]Enter URL of the cloud image to use for VM template (default: Debian v13)[/]", default=image_url, show_default=False)
image_name = image_url.split("/")[-1]
c.log(f"[blue bold]Downloading image from {image_url}[/]")
image_volid = f'{vdisk_storage}:import/{image_name}'
image_vol = prox.nodes('pve').storage(vdisk_storage).content(image_volid).get()

if image_vol is not None and image_vol['format'] == 'qcow2':
    c.log(f"[green]Image already exists in storage {vdisk_storage}[/]")
else:
    task = prox.nodes('pve').storage(vdisk_storage)('download-url').post(
        content='import',
        url=image_url,
        filename=image_name)
    ph.await_task(prox, task)
    
# Get ssh-keys (either rsa or ed25519) from ssh user folder to inject into VM for later k3sup usage
ssh_keys, ssh_key_files = ph.get_ssh_publickey()
# url encode ssh keys for cloud-init user-data injection
ssh_keys = urllib.parse.quote(ssh_keys)
# Ask the user to enter ssh private key file path
ssh_priv_key = Prompt.ask("[green bold]Enter path to SSH private key file[/]", default="C:\\Users\\ankurmathur01\\.ssh\\id_ed25519")

# Get gateway fields from user input for cloud-init configuration
gateway = Prompt.ask("[green bold]Enter network gateway (e.g. 192.168.1.1)[/]", default="192.168.1.1")

# Get number of worker nodes to create
agents = Prompt.ask("[green bold]Enter number of worker nodes to create[/]", default="2", show_default=True)

# Create k3s VM
# is_control: control plane nodes get more cores and ballooning disabled
def create_k3s_vm(name, ip_address, gateway, disk_size='10G', is_control=False):
    nextid = int(str(prox.cluster.nextid.get()))
    c.log(f"[blue]Creating VM with ID {nextid} from template, this may take a few minutes...[/]")
    task = prox.nodes('pve').qemu.post(
        name=name,
        vmid=nextid,
        agent=1,
        balloon=0,
        memory=4096 if is_control else 6144,
        boot='order=scsi0;net0',
        cipassword='Ankank29',
        ciuser='k3s',
        sockets=1,
        cores=1 if is_control else 2,
        cpu='host',
        ide2=f'{vm_disk_storage}:cloudinit',
        ipconfig0=f'ip={ip_address}/24,gw={gateway}',
        machine='q35',
        ostype='l26',
        scsi0=f'{vm_disk_storage}:0,import-from={image_volid},ssd=1',
        scsihw='virtio-scsi-pci',
        vga='serial0',
        net0='virtio,bridge=vmbr0,firewall=1',
        sshkeys=ssh_keys,
        serial0='socket'
    )

    ph.await_task(prox, task)
    c.log(f"[green]VM {name} created with ID {nextid}[/]")
    c.log(f"[blue]Resizing disk to {disk_size}...[/]")
    # Resize the disk to the specified size to have enough space for k3s and some workloads, adjust as needed
    task = prox.nodes('pve').qemu(nextid).resize.put(
        disk='scsi0',
        size=disk_size
    )
    ph.await_task(prox, task)
    c.log(f"[green]Disk resized to {disk_size}[/]")
    return nextid

c.rule(f"[bold blue]Creating k3s cluster control node VM[/]")
ip_address = Prompt.ask("[green bold]Enter static IP address for the control-plane VM (e.g. 192.168.1.210)[/]")
vm_name = "k3s-control"
vm_id = create_k3s_vm(vm_name, ip_address, gateway, is_control=True)
c.log(f"[green]VM {vm_name} created with ID {vm_id} and IP address {ip_address}[/]")

agents_vm_info = []
for i in range(1, int(agents) + 1):
    c.rule(f"[bold blue]Creating k3s cluster worker{i} node VM[/]")
    agent_ip_address = Prompt.ask(f"[green bold]Enter static IP address for the worker{i} VM[/]")
    agent_vm_name = f"k3s-worker{i}"
    agent_vm_id = create_k3s_vm(agent_vm_name, agent_ip_address, gateway)
    agents_vm_info.append({"vmid": agent_vm_id, "name": agent_vm_name, "ip": agent_ip_address})
    c.log(f"[green]VM {agent_vm_name} created with ID {agent_vm_id} and IP address {agent_ip_address}[/]")

# Start the control VM and install k3s with k3sup, then join worker nodes to the cluster
# Also, install qemu guest agent for better integration and management of the VMs
# Start VM
c.rule(f"[bold blue]Running initial setup on control plane VM[/]")
initial_cmd = """
sudo apt install qemu-guest-agent open-iscsi nfs-common cifs-utils -y
sudo systemctl enable --now iscsid

# Fix iscsiadm path for democratic-csi (on some distros it's not in the default PATH for root, which is what cloud-init uses to run user scripts, so we create a symlink)
sudo ln -sf /usr/sbin/iscsiadm /usr/local/sbin/iscsiadm

# Verify
sudo systemctl is-active iscsid
sudo /usr/local/sbin/iscsiadm --version

sudo reboot now
"""
ph.start_vm(prox, vm_id, ip_address)
ph.run_script_over_ssh(ip_address, initial_cmd, title="Initial script installation")
ph.wait_for_ssh(ip_address)  # Wait for VM to come back up after reboot

for agent in agents_vm_info:
    ph.start_vm(prox, agent['vmid'], agent['ip'])
    ph.run_script_over_ssh(agent['ip'], initial_cmd, title="Initial script installation")
    ph.wait_for_ssh(agent['ip'])  # Wait for VM to come back up after reboot
    
# Execute k3sup install
# Get ssh key path (either rsa or ed25519) from ssh user folder to use for k3sup installation
c.rule("[bold blue]Installing k3s with k3sup on control plane[/]")
args = ["--ip", ip_address, "--user", "k3s"]
args.extend(["--ssh-key", ssh_priv_key])
with local.cwd(deployment_cluster_dir):
    k3sup_cmd = local["k3sup"]["install"][args + ["--k3s-extra-args", "--disable traefik --disable servicelb"]]
    future = k3sup_cmd & BG
    if future.stdout is not None:
        for line in future.stdout:
            c.print(line, end="", style="dim")
    future.wait()
    c.rule(f"[bold green]✅ Done")

    for agent in agents_vm_info:
        c.rule(f"[bold blue]Joining {agent['name']} to the cluster with k3sup[/]")
        join_args = ["--ip", agent['ip']]
        join_args.extend(["--server-ip", ip_address])
        join_args.extend(["--user", "k3s", "--server-user", "k3s"])
        join_args.extend(["--ssh-key", ssh_priv_key])
        k3sup_join_cmd = local["k3sup"]["join"][join_args]
        future = k3sup_join_cmd & BG
        if future.stdout is not None:
            for line in future.stdout:
                c.print(line, end="", style="dim")
        future.wait()

c.log(f"[yellow bold]⚠️Test your cluster with the generated kubeconfig file:\nSaving file to: {Path('./').absolute().joinpath('kubeconfig')}\n[/]")

kubeconfig_env = local \
    .cwd(deployment_scripts_dir) \
    .env(KUBECONFIG=f"{deployment_cluster_dir.absolute().joinpath('kubeconfig')}")
with kubeconfig_env:
    c.rule(f"[bold blue]Tainting control plane node to prevent scheduling regular workloads[/]")
    _ = local["kubectl"]["taint", "nodes", "k3s-control", "node-role.kubernetes.io/control-plane=:NoSchedule", "--overwrite"] & FG
    
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
        helm install metallb metallb/metallb --namespace metallb-system -f .\\metallb\\helm-metallb-values.yaml --wait
        kubectl apply -f .\\metallb\\metallb-config.yaml
    }"""]
    future = metallb_cmd & BG
    while not future.poll():
        if future.stdout is not None:
            for line in future.stdout:
                c.print(line, end="", style="grey50 dim")
    future.wait()
    c.rule(f"[bold green]✅ MetalLB Installed")

    # Ask the user whether they want to install Democratic CSI for TrueNAS integration, and if yes, ask for the required configuration values and install it with kustomize and helm
    install_csi = Prompt.ask("[green bold]Do you want to install Democratic CSI for TrueNAS integration? (y/n)[/]", choices=["y", "n"], default="y")
    if install_csi == "y":
        c.rule(f"[bold blue]Setting up Democratic CSI[/]")
        # Before installing Democratic CSI, we need to setup 
        # TrueNAS by creating the necessary datasets for NFS and iSCSI, and generating an API key for the CSI driver to authenticate with TrueNAS API.
        # This can be automated with a script that uses the TrueNAS API, but for simplicity we will ask the user to do it manually and provide the required values.
        c.log("[yellow bold]⚠️Please set up your TrueNAS with the following configuration before proceeding:[/]")
        c.print(Text("- Create a ZFS dataset for NFS volumes (e.g. pool1/k8s-nfs)", style="green"))
        c.print(Text("- Create a ZFS dataset for iSCSI volumes (e.g. pool1/pool1-iscsi)", style="green"))
        c.print(Text("- Generate an API key for the CSI driver with permissions to manage storage and access (you can create a new user for this and assign the appropriate permissions, then generate an API key for that user)", style="green"))
        c.print(Text("- Note down the IP address (or hostname) of your TrueNAS and the allowed network CIDR for NFS access (e.g. 192.168.1.0/24)", style="green"))
        
        # TrueNAS / democratic-csi configuration
        c.rule("[bold blue]TrueNAS Storage Configuration[/]")
        truenas_ip = Prompt.ask("[green bold]Enter TrueNAS IP address (or hostname)[/]", default="192.168.1.18")
        truenas_api_key = Prompt.ask("[green bold]Enter TrueNAS API key[/]", password=True)
        nfs_dataset_parent = Prompt.ask("[green bold]Enter ZFS parent dataset for NFS volumes[/]", default="pool1/k8s-nfs")
        iscsi_dataset_parent = Prompt.ask("[green bold]Enter ZFS parent dataset for iSCSI volumes[/]", default="pool1/pool1-iscsi")
        truenas_network_cidr = Prompt.ask("[green bold]Enter allowed network CIDR for NFS access[/]", default=f"{gateway.rsplit('.', 1)[0]}.0/24")

        # Patch the committed template files with runtime values and write the gitignored output files.
        # Templates (config-*.yaml.tpl) define the config structure — Python only fills in the placeholders.
        # Kustomize secretGenerator reads the output files and wraps them into K8s Secrets.
        overlay_path = Path(".") / "democratic-csi" / "overlays" / "local-truenas"

        substitutions = {
            "TRUENAS_IP":          truenas_ip,
            "TRUENAS_API_KEY":     truenas_api_key,
            "NFS_DATASET_PARENT":  nfs_dataset_parent,
            "ISCSI_DATASET_PARENT": iscsi_dataset_parent,
            "TRUENAS_NETWORK_CIDR": truenas_network_cidr,
        }

        for tpl_name, out_name in [("config-nfs.yaml.tpl", "config-nfs.yaml"),
                                    ("config-iscsi.yaml.tpl", "config-iscsi.yaml")]:
            tpl_text = (overlay_path / tpl_name).read_text()
            patched = tpl_text.format_map(substitutions)
            (overlay_path / out_name).write_text(patched)
            c.log(f"[green]Patched {tpl_name} → {out_name}[/]")

        csi_cmd = local["pwsh"]["-Command", """&{
            kubectl kustomize .\\democratic-csi\\overlays\\local-truenas --enable-helm | kubectl apply -f -
        }"""]
        future = csi_cmd & BG
        while not future.poll():
            if future.stdout is not None:
                for line in future.stdout:
                    c.print(line, end="", style="grey50 dim")
        future.wait()
        c.rule(f"[bold green]✅ Democratic CSI Installed")
    
    c.log(f"[yellow bold]⚠️Your cluster is ready! Test it with: kubectl get nodes --kubeconfig {os.environ.get('KUBECONFIG')}[/]")
    
    # Bootstrap and start deployment with ArgoCD. 
    # You can also use kubectl directly to deploy applications or manage the cluster.
    with local.cwd(deployment_cluster_dir):
        c.rule(f"[bold blue]Bootstrapping ArgoCD and starting application deployment[/]")
        local['py'][".\\bootstrap.py"].run()
        c.rule(f"[bold green]✅ ArgoCD Bootstrapped and Application Deployment Started")