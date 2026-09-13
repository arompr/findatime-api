.PHONY: build container-staging container-dev typescript-client

build:
	podman build -t findatime-api .

typescript-client:
	dotnet build src/FindAtime.Api/FindAtime.Api.csproj -c Release
	cd typescript-client && npm install && npm run prepare

container-staging: build
	podman run --rm -p 5263:8080 --env-file .env.staging findatime-api

container-dev: build
	podman run --rm --network=host --env-file .env findatime-api