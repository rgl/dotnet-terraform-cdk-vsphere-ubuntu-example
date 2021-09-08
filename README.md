# About

**This is a WIP** and this branch will be rebased.

Manage example vSphere Virtual Machines using dotnet [terraform-cdk](https://github.com/hashicorp/terraform-cdk).

For terrafom hcl see the [rgl/terraform-vsphere-ubuntu-example repository](https://github.com/rgl/terraform-vsphere-ubuntu-example).

## Usage

Install the [Ubuntu 20.04 VM template](https://github.com/rgl/ubuntu-vagrant).

Install Terraform and govc (Ubuntu):

```bash
wget https://releases.hashicorp.com/terraform/1.0.6/terraform_1.0.6_linux_amd64.zip
unzip terraform_1.0.6_linux_amd64.zip
sudo install terraform /usr/local/bin
rm terraform terraform_*_linux_amd64.zip
wget https://github.com/vmware/govmomi/releases/download/v0.26.1/govc_Linux_x86_64.tar.gz
tar xf govc_Linux_x86_64.tar.gz govc
sudo install govc /usr/local/bin/govc
rm govc govc_Linux_x86_64.tar.gz
```

Install node 14.x and the dotnet 5 SDK.

Install this project dependencies and build it:

```bash
npm install
npm run-script build
```

Save your environment details as a script that sets the terraform variables from environment variables, e.g.:

```bash
cat >secrets.sh <<'EOF'
export VSPHERE_USER='administrator@vsphere.local'
export VSPHERE_PASSWORD='password'
export VSPHERE_SERVER='vsphere.local'
export VSPHERE_DATACENTER='Datacenter'
export VSPHERE_COMPUTE_CLUSTER='Cluster'
export VSPHERE_DATASTORE='Datastore'
export VSPHERE_NETWORK='VM Network'
export VSPHERE_FOLDER="examples/terraform-cdk-ubuntu-example"
export VSPHERE_UBUNTU_TEMPLATE='vagrant-templates/ubuntu-20.04-amd64-vsphere'
export VM_COUNT='1'
export GOVC_INSECURE='1'
export GOVC_URL="https://$VSPHERE_SERVER/sdk"
export GOVC_USERNAME="$VSPHERE_USER"
export GOVC_PASSWORD="$VSPHERE_PASSWORD"
EOF
```

**NB** You could also add these variables definitions into the `terraform.tfvars` file, but I find the environment variables more versatile as they can also be used from other tools, like govc.

Launch this example:

```bash
source secrets.sh
# see https://github.com/vmware/govmomi/blob/master/govc/USAGE.md
govc version
govc about
govc datacenter.info # list datacenters
govc find # find all managed objects
dotnet build
npx cdktf diff
npx cdktf deploy
# TODO test from here on...
ssh-keygen -f ~/.ssh/known_hosts -R "$(terraform output --json ips | jq -r '.[0]')"
ssh "vagrant@$(terraform output --json ips | jq -r '.[0]')"
time terraform destroy --auto-approve
```

Destroy everything:

```bash
npx cdktf destroy
```
