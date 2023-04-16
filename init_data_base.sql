create
    database if not exists tg_data;
use
    tg_data;

create table if not exists msg
(
    id         bigint auto_increment,
    msg_id     int           not null default 0,
    user_id    bigint        not null default 0,
    channel_id bigint        not null default 0,
    send_time  timestamp     not null default now(),
    content    varchar(5120) not null default '',
    fulltext (content) with parser ngram,
    constraint id
        primary key (id)
) Engine = InnoDB;


create table if not exists user
(
    id          bigint auto_increment,
    user_id     bigint      not null default 0,
    last_name   varchar(64) not null default '',
    first_name  varchar(64) not null default '',
    is_bot      boolean     not null default false,
    access_hash bigint      not null default 0,
    constraint id
        primary key (id)
) Engine = InnoDB;

create table if not exists channel
(
    id              bigint auto_increment,
    channel_id      bigint      not null default 0,
    title           varchar(64) not null default '',
    is_fill_history boolean     not null default false,
    access_hash     bigint      not null default 0,
    constraint id
        primary key (id)
) Engine = InnoDB;

create unique index msg_id_index
    on msg (msg_id);

create index channel_id_index
    on msg (channel_id);

create unique index user_id_index
    on user (user_id);

create unique index channel_id_index
    on channel (channel_id);

use mysql;
update user
set Host='192.168.2.%'
where User = 'root';

create user 'username'@'%' identified by 'password';
grant all privileges on tg_data.* to 'username'@'%';

alter user 'username'@'%' identified by 'user_password';

flush privileges;
