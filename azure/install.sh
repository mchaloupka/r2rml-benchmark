#!/bin/sh
echo "Prepare for install"
apt update
apt upgrade -y

echo "Change directory"
mkdir /autodeploy
cd /autodeploy

echo "Install dotnet"
wget https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb

apt-get update; \
apt-get install -y apt-transport-https && \
apt-get update && \
apt-get install -y dotnet-sdk-5.0

echo "Install docker"
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
rm get-docker.sh

echo "Pull r2rml-benchmark repository"
git clone https://github.com/mchaloupka/r2rml-benchmark.git
