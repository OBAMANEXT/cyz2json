DATE=$(shell date +%Y%m%d)

.PHONY: run release clean

bin/Cyz2Json.dll : Cyz2Json/Program.cs
	dotnet build -o bin

run: bin/Cyz2Json.dll
	dotnet bin/Cyz2Json.dll

release: bin/Cyz2Json.dll
	cp -r bin cyz2json-$(DATE)
	zip -r cyz2json-$(DATE).zip cyz2json-$(DATE)
	rm -rf cyz2json-$(DATE)

clean:
	rm -rf bin
	rm -f *.zip
