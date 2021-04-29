#!/bin/sh
echo "Prepare for install"
apt update
apt upgrade -y

echo "Change directory"
cd ~/

echo "Install docker"
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
rm get-docker.sh

echo "Pull r2rml-benchmark repository"
git clone https://github.com/mchaloupka/r2rml-benchmark.git
