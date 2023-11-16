# Install on linux

### Install packages

`sudo apt install gcc openmpi-bin openmpi-common libopenmpi-dev`

### Compile
```
dotnet build
```

###  Extract dependencies
Extract `MPI_Deps.zip` in `bin/Debug/net7.0`

### Run
```
mpiexec -n 2 -xterm -1 dotnet ./bin/Debug/net7.0/snapshot-chat.dll
```