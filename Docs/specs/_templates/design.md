# <Feature> Design

## Overview

<how this feature is designed in this codebase — aggregate boundaries, bounded
context, key relationships.>

## Domain Model

<relationships between entities, e.g.>

    Entity 1 ──── 0..* Entity2

## API

<endpoint shapes>

## Persistence

<storage model — tables, key columns>

## Validation

<significant validation rules>

## Application Flow

<call path, e.g. API → UseCase → Repository → PostgreSQL>

## Testing

<test strategy split, e.g. unit / integration / e2e>

---

Keep this to decisions an agent can't reliably infer from one or two files:
aggregate boundaries, important relationships, API shape, persistence model,
architectural constraints, significant validation rules, integration points,
non-obvious decisions. Do not document line-level implementation details.
