# Running Docker

## Environment vairables

Required environments are in `.env.template` files, in order to run docker compose you need to create `.env` file basing on template.

Current Evironments sets:
- `api` - Environments variables for api
- `smtp` - Environment variables used in smtp server
- `database` - Environment variables used in database
- `messagebus` - Environment variables used in message bus
- `seed` - Environment variables for seeding

### HTTPS

The API container serves all API traffic (REST, gRPC, gRPC-Web) over HTTPS only, on container port `8443`
(host port `API_HTTPS_PORT`), with HTTP/1.1 and HTTP/2. Kestrel needs TLS for HTTP/2, so native gRPC works only over HTTPS.
Port `5001` (host port `API_HEALTH_PORT`) stays plain HTTP for the health check.

Before the first `docker compose up`, generate the certificate (run in the `docker` folder):

```bash
sh generate-cert.sh
```

It writes a self-signed `https/senswave.api.pfx` (served by Kestrel) and `https/senswave.api.crt`, valid for
`api`, `localhost` and `127.0.0.1`. Pass a password as the first argument to change the default `password`, and set the
same value in `API_CERTIFICATE_PASSWORD`. The seed container trusts the certificate through `SSL_CERT_FILE`.
To call the API from the host, trust `https/senswave.api.crt` or pass it to your client (e.g. `curl --cacert https/senswave.api.crt`).

### Seed

The seed container registers the users from the `Seed__*` variables through the API after it becomes healthy.
Besides `Seed__DefaultUser`, it can generate a batch of users from one template: `Seed__GeneratedUsers__Count` users
named by `Seed__GeneratedUsers__EmailPattern` (`{0}` is the 1-based index), all with the same password, home, devices
and automations, and all pointing at one shared MQTT broker given by `Seed__GeneratedUsers__Broker__*`. Each user
gets a unique broker client id, and `{0}` in `Seed__GeneratedUsers__Devices__PicoController__GlobalTopic` keeps the
controller topics apart.

Several users registering the same broker URL and port is rejected unless
`Modules__DataSource__Brokers__AllowSharedBrokers=true` is set on the API, which the template does. Seeding is
sequential and tests every broker connection, so 100 users take a few minutes.

## Run Environment

You can adjust ports in docker-compose on in .env.
Command: `docker-compose up -d`.

