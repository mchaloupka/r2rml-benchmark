# Running Benchmark on Azure

First, it is necessary to deploy all necessary hardware. That can be achieved
by using the Azure custom template.

To do so, use `Deploy Custom Template` functionality. When deploying from the template, you have to:

* Select the subscription.
* Ideally create a new resource group so you can easily clean-up everything afterwards.
* Select region - may select according to pricing or one that is close to you.
* Either select password or SSH public key for accessing the machine.
