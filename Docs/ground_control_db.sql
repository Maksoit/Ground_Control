create table nodes (
  node_id   text primary key,
  x         numeric not null,
  y         numeric not null,
  node_type text
);

create table edges (
  edge_id   text primary key,
  from_node text not null,
  to_node   text not null,
  length    numeric not null default 1
);

create index edges_from_idx on edges(from_node);
create index edges_to_idx on edges(to_node);

create table routes (
  route_id     uuid primary key,
  vehicle_id   text not null,
  vehicle_type text not null check (vehicle_type in ('plane','bus')),
  from_node    text not null,
  to_node      text not null,
  edges_path   jsonb not null,

  status       text not null check (status in ('allocated','in_use','finished','rejected')),
  created_at   timestamp not null default now(),
  updated_at   timestamp not null default now()
);

create index routes_vehicle_idx on routes(vehicle_id);


create table edge_occupancy (
  edge_id     text primary key,
  occupied_by text not null,     
  route_id    uuid,
  updated_at  timestamp not null default now()
);

create table processed_events (
  event_id     uuid primary key,
  processed_at timestamp not null default now()
);
