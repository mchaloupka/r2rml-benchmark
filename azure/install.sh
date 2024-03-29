#!/bin/sh
USER=$1

echo "Prepare for install"
apt update
apt upgrade -y

echo "Change directory"
mkdir /autodeploy
cd /autodeploy

echo "Install dotnet"
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x ./dotnet-install.sh
sudo -u $USER ./dotnet-install.sh --version latest
rm ./dotnet-install.sh

echo "Install docker"
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
rm get-docker.sh

echo "Pull r2rml-benchmark repository"
git clone https://github.com/mchaloupka/r2rml-benchmark.git

echo "Change ownership of folder"
sudo chown -R $USER: /autodeploy

echo "Add user to docker group"
sudo usermod -aG docker $USER
newgrp docker

echo "Install tmux"
apt install tmux

echo '' >> /home/$USER/.bashrc 
echo 'export DOTNET_ROOT=$HOME/.dotnet' >> /home/$USER/.bashrc 
echo 'export PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools' >> /home/$USER/.bashrc 
