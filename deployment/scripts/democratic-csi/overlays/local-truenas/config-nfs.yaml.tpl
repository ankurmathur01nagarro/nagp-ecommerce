driver: freenas-api-nfs
httpConnection:
  protocol: http
  host: {TRUENAS_IP}
  port: 80
  apiKey: "{TRUENAS_API_KEY}"
  allowInsecure: true
zfs:
  datasetParentName: {NFS_DATASET_PARENT}
  detachedSnapshotsDatasetParentName: {NFS_DATASET_PARENT}-snapshots
  datasetEnableQuotas: true
  datasetPermissionsMode: "0777"
nfs:
  shareHost: {TRUENAS_IP}
  shareAllowedNetworks:
    - "{TRUENAS_NETWORK_CIDR}"
  shareMaprootUser: root
  shareMaprootGroup: root
