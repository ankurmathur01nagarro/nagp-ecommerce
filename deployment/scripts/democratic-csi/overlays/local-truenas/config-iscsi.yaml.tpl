driver: freenas-api-iscsi
httpConnection:
  protocol: http
  host: {TRUENAS_IP}
  port: 80
  apiKey: "{TRUENAS_API_KEY}"
  allowInsecure: true
zfs:
  datasetParentName: {ISCSI_DATASET_PARENT}
  detachedSnapshotsDatasetParentName: {ISCSI_DATASET_PARENT}-snapshots
  datasetEnableQuotas: true
  datasetPermissionsMode: "0777"
iscsi:
  targetPortal: "{TRUENAS_IP}:3260"
  namePrefix: "csi-"
  nameSuffix: "-k3s"
  targetGroups:
    - targetGroupPortalGroup: 1
      targetGroupInitiatorGroup: 1
      targetGroupAuthType: None
  extentInsecureTpc: true
  extentDisablePhysicalBlocksize: true
  extentBlocksize: 512
  extentRpm: "SSD"
