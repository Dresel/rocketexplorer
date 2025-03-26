dotnet tool restore
cd src/RocketExplorer.Ethereum

dotnet tool run Nethereum.Generator.Console generate from-abi -abi abis\addressQueueStorage.json -o ./Clients -cn AddressQueueStorage -ns RocketExplorer.Ethereum -sf true
dotnet tool run Nethereum.Generator.Console generate from-abi -abi abis\addressSetStorage.json -o ./Clients -cn AddressSetStorage -ns RocketExplorer.Ethereum -sf true
dotnet tool run Nethereum.Generator.Console generate from-abi -abi abis\linkedListStorage.json -o ./Clients -cn LinkedListStorage -ns RocketExplorer.Ethereum -sf true
dotnet tool run Nethereum.Generator.Console generate from-abi -abi abis\rocketStorage.json -o ./Clients -cn RocketStorage -ns RocketExplorer.Ethereum -sf true
dotnet tool run Nethereum.Generator.Console generate from-abi -abi abis\rocketDAONodeTrustedUpgrade.json -o ./Clients -cn RocketDAONodeTrustedUpgrade -ns RocketExplorer.Ethereum -sf true
dotnet tool run Nethereum.Generator.Console generate from-abi -abi abis\rocketNodeManager.json -o ./Clients -cn RocketNodeManager -ns RocketExplorer.Ethereum -sf true
dotnet tool run Nethereum.Generator.Console generate from-abi -abi abis\rocketMegapoolDelegate.json -o ./Clients -cn RocketMegapoolDelegate -ns RocketExplorer.Ethereum -sf true