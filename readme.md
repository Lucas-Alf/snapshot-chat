### Install RabbitMQ
Run the following command and wait some seconds for the container creation
```
docker-compose up -d
```

### Compile
```
dotnet build
```

### Run
```
dotnet ./bin/Debug/net7.0/snapshot-chat.dll
```