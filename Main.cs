using System;
using Constructs;
using HashiCorp.Cdktf;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using vsphere;
using template;

class ExampleStack : TerraformStack
{
    public ExampleStack(Construct scope, string id)
        : base(scope, id)
    {
        var vsphereUser = Environment.GetEnvironmentVariable("VSPHERE_USER");
        var vspherePassword = Environment.GetEnvironmentVariable("VSPHERE_PASSWORD");
        var vsphereServer = Environment.GetEnvironmentVariable("VSPHERE_SERVER");
        var datacenterName = Environment.GetEnvironmentVariable("VSPHERE_DATACENTER");
        var computeClusterName = Environment.GetEnvironmentVariable("VSPHERE_COMPUTE_CLUSTER");
        var datastoreName = Environment.GetEnvironmentVariable("VSPHERE_DATASTORE");
        var networkName = Environment.GetEnvironmentVariable("VSPHERE_NETWORK");
        var folderPath = Environment.GetEnvironmentVariable("VSPHERE_FOLDER");
        var ubuntuTemplateName = Environment.GetEnvironmentVariable("VSPHERE_UBUNTU_TEMPLATE");
        var vmCount = int.Parse(Environment.GetEnvironmentVariable("VM_COUNT") ?? "1");
        var vmCpu = int.Parse(Environment.GetEnvironmentVariable("VM_CPU") ?? "2");
        var vmMemory = int.Parse(Environment.GetEnvironmentVariable("VM_MEMORY") ?? "1");
        var vmDiskOsSize = int.Parse(Environment.GetEnvironmentVariable("VM_DISK_OS_SIZE") ?? "10");
        var vmDiskDataSize = int.Parse(Environment.GetEnvironmentVariable("VM_DISK_DATA_SIZE") ?? "1");
        var vmHostnamePrefix = Environment.GetEnvironmentVariable("VM_HOSTNAME_PREFIX");

        var sshPublicKeyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh/id_rsa.pub");
        var sshPublicKeyJson = JsonSerializer.Serialize(System.IO.File.ReadAllText(sshPublicKeyPath).Trim());

        new VsphereProvider(this, "vsphere", new VsphereProviderConfig
        {
            User = vsphereUser,
            Password = vspherePassword,
            VsphereServer = vsphereServer,
            AllowUnverifiedSsl = true,
        });

        var datacenter = new DataVsphereDatacenter(this, "datacenter", new DataVsphereDatacenterConfig
        {
            Name = "/" + datacenterName,
        });

        var computeCluster = new DataVsphereComputeCluster(this, "compute_cluster", new DataVsphereComputeClusterConfig{
            Name = computeClusterName,
            DatacenterId = datacenter.Id,
        });

        var datastore = new DataVsphereDatastore(this, "datastore", new DataVsphereDatastoreConfig{
            Name = datastoreName,
            DatacenterId = datacenter.Id,
        });

        var network = new DataVsphereNetwork(this, "network", new DataVsphereNetworkConfig
        {
            Name = networkName,
            DatacenterId = datacenter.Id,
        });

        var ubuntuTemplate = new DataVsphereVirtualMachine(this, "ubuntu_template", new DataVsphereVirtualMachineConfig
        {
            Name = ubuntuTemplateName,
            DatacenterId = datacenter.Id,
        });

        var folder = new Folder(this, "folder", new FolderConfig
        {
            Path = folderPath,
            Type = "vm",
            DatacenterId = datacenter.Id,
        });

        Func<string, int, VirtualMachine> vm = (name, memory) =>
        {
            var cloudInit = new DataTemplateCloudinitConfig(this, $"{name}-cloud-init", new DataTemplateCloudinitConfigConfig
            {
                Gzip = true,
                Base64Encode = true,
                Part = new[]
                {
                    new DataTemplateCloudinitConfigPart
                    {
                        ContentType = "text/cloud-config",
                        Content = $@"
#cloud-config
hostname: {name}
users:
  - name: vagrant
    passwd: '$6$rounds=4096$NQ.EmIrGxn$rTvGsI3WIsix9TjWaDfKrt9tm3aa7SX7pzB.PSjbwtLbsplk1HsVzIrZbXwQNce6wmeJXhCq9YFJHDx9bXFHH.'
    lock_passwd: false
    ssh-authorized-keys:
      - {sshPublicKeyJson}
disk_setup:
  /dev/sdb:
    table_type: mbr
    layout:
      - [100, 83]
    overwrite: false
fs_setup:
  - label: data
    device: /dev/sdb1
    filesystem: ext4
    overwrite: false
mounts:
  - [/dev/sdb1, /data, ext4, 'defaults,discard,nofail', '0', '2']
runcmd:
  - sed -i '/vagrant insecure public key/d' /home/vagrant/.ssh/authorized_keys
  # make sure the vagrant account is not expired.
  # NB this is needed when the base image expires the vagrant account.
  - usermod --expiredate '' vagrant
",
                    },
                },
            });

            return new VirtualMachine(this, name, new VirtualMachineConfig
            {
                Folder = folder.Path,
                Name = name,
                GuestId = ubuntuTemplate.GuestId,
                NumCpus = vmCpu,
                NumCoresPerSocket = vmCpu,
                Memory = memory,
                EnableDiskUuid = true,
                ResourcePoolId = computeCluster.Id,
                DatastoreId = datastore.Id,
                ScsiType = ubuntuTemplate.ScsiType,
                Disk = new[]
                {
                    new VirtualMachineDisk
                    {
                        UnitNumber = 0,
                        Label = "os",
                        Size = Math.Max(ubuntuTemplate.Disks("0").Size, vmDiskOsSize),
                        EagerlyScrub = ubuntuTemplate.Disks("0").EagerlyScrub,
                        ThinProvisioned = ubuntuTemplate.Disks("0").ThinProvisioned,
                    },
                    new VirtualMachineDisk
                    {
                        UnitNumber = 1,
                        Label = "data",
                        Size = vmDiskDataSize,
                        EagerlyScrub = ubuntuTemplate.Disks("0").EagerlyScrub,
                        ThinProvisioned = ubuntuTemplate.Disks("0").ThinProvisioned,
                    },
                },
                NetworkInterface = new[]
                {
                    new VirtualMachineNetworkInterface
                    {
                        NetworkId = network.Id,
                        AdapterType = ubuntuTemplate.NetworkInterfaceTypes[0],
                    },
                },
                Clone = new[]
                {
                    new VirtualMachineClone
                    {
                        TemplateUuid = ubuntuTemplate.Id,
                    },
                },
                ExtraConfig = new Dictionary<string, string>
                {
                    {"guestinfo.userdata", cloudInit.Rendered},
                    {"guestinfo.userdata.encoding", "gzip+base64"},
                },
                // TODO how to add a provisioner "remote-exec"?
            });
        };

        var vms = Enumerable.Range(0, vmCount)
            .Select(index => vm($"example{index}", vmMemory*1024))
            .ToList();
    }

    public static void Main(string[] args)
    {
        var app = new App();
        new ExampleStack(app, "example");
        app.Synth();
        Console.WriteLine("App synth complete");
    }
}