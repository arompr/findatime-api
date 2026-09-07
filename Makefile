.PHONY: build container-staging container-dev

build:
	podman build -t findatime-api .

container-staging: build
	podman run --rm -p 5263:8080 --env-file .env.staging findatime-api

container-dev: build
	podman run --rm --network=host --env-file .env findatime-api