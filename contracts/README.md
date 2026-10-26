# Contracts

This folder holds the message contracts shared by `apps/jobs-api` (producer) and
`apps/ml-worker` (consumer). Both sides have a **contract test** that checks their
own message model against these schemas, so a breaking change fails the unit
tests of whichever side drifted.

| Message | Schema | Exchange | Routing key | Queue |
|---|---|---|---|---|
| `job.requested` v1 | [`job-requested.v1.schema.json`](job-requested.v1.schema.json) | `jobs` (direct) | `job.requested` | `ml-worker.jobs` (quorum) |

## RabbitMQ topology

Both services declare the topology at startup. Declarations are idempotent, but
the arguments **must be identical** on both sides:

```
exchange  jobs       (direct, durable)
exchange  jobs.dlx   (fanout, durable)
queue     ml-worker.jobs      (quorum, x-dead-letter-exchange=jobs.dlx)  <- bound to jobs / job.requested
queue     ml-worker.jobs.dlq  (quorum)                                   <- bound to jobs.dlx
```

Messages the worker cannot parse or validate are rejected without requeue, so
they land in `ml-worker.jobs.dlq` for inspection.

## Evolving a contract

- **Additive, optional fields:** bump nothing. Consumers must ignore unknown fields.
- **Breaking changes:** add `job-requested.v2.schema.json` and a new
  `schema_version`. Run both versions side by side until every consumer is migrated.
