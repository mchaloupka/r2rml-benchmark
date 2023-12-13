# Running Benchmark on Azure

## Deploying

First, it is necessary to deploy all necessary hardware. That can be achieved
by using the Azure custom template.

To do so, use `Deploy Custom Template` functionality. Use edit or upload the template, and provide the content of the `azuredeploy.json` file.

When deploying from the template, you have to:

* Select the subscription.
* Ideally create a new resource group so you can easily clean-up everything afterwards.
* Select region - may select according to pricing or one that is close to you.
* Either select password or SSH public key for accessing the machine.
* You can optionally change the user name, but feel free to leave the default `bsbmadmin`.

After deployment is complete, you can find your VM and its public IP address in the VM overview. The VM will be named `r2rmlBenchmark`.

## Connecting to machine

You can connect using `ssh <user>@<ipaddress>` where `<ipaddress>` is the public IP you retrieved in previous step and the `<user>` is the user to use connect. By default the user is `bsbmadmin` unless you have changed it during deployment.

## Running benchmark

In the folder `/autodeploy/r2rml-benchmark`, the r2rml benchmark repository is cloned, the .NET SDK 8 is installed and docker is also available.

We recommend running the benchmark using tmux, as it can run even after the ssh connectivity is closed.

To enter tmux, just run `tmux` command.

To run the benchmark, just run the `dotnet fsi main.fsx`.

You can detach from your tmux session by pressing `Ctrl+B` then `D`. Then you can even disconnect from the machine and the benchmark will be still running. Using command `tmux ls` it is possible to list active sessions, and then using `tmux attach -t <id>` to attach again to a seesion `<id>` (most likely 0 if you have just the one you detached before).
