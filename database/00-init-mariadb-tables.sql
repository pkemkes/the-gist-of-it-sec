CREATE DATABASE IF NOT EXISTS thegistofitsec;

USE thegistofitsec;

CREATE TABLE IF NOT EXISTS feeds (
    id INT AUTO_INCREMENT PRIMARY KEY,
    title TEXT NOT NULL,
    link MEDIUMTEXT NOT NULL,
    rss_link MEDIUMTEXT NOT NULL,
    language TINYTEXT
);

CREATE INDEX IF NOT EXISTS feeds_by_link ON feeds(link);

CREATE TABLE IF NOT EXISTS gists (
    id INT AUTO_INCREMENT PRIMARY KEY,
    reference TEXT NOT NULL,
    feed_id INT NOT NULL,
    author TEXT NOT NULL,
    title TEXT NOT NULL,
    published DATETIME NOT NULL,
    updated DATETIME NOT NULL,
    link MEDIUMTEXT NOT NULL,
    summary LONGTEXT NOT NULL,
    tags LONGTEXT NOT NULL,
    search_query MEDIUMTEXT NOT NULL,
    disabled BOOLEAN DEFAULT FALSE,
    FOREIGN KEY (feed_id) REFERENCES feeds(id),
    FULLTEXT(summary),
    FULLTEXT(tags)
);

CREATE INDEX IF NOT EXISTS gists_by_reference ON gists(reference);
CREATE INDEX IF NOT EXISTS gists_by_updated ON gists(updated);

CREATE TABLE IF NOT EXISTS chats (
    id INT PRIMARY KEY,
    gist_id_last_sent INT,
    FOREIGN KEY (gist_id_last_sent) REFERENCES gists(id)
);

CREATE TABLE IF NOT EXISTS search_results (
    id INT AUTO_INCREMENT PRIMARY KEY,
    gist_id INT NOT NULL,
    title TEXT NOT NULL,
    snippet MEDIUMTEXT NOT NULL,
    link MEDIUMTEXT NOT NULL,
    display_link TEXT NOT NULL,
    thumbnail_link MEDIUMTEXT,
    image_link MEDIUMTEXT,
    FOREIGN KEY (gist_id) REFERENCES gists(id)
);

CREATE TABLE IF NOT EXISTS recaps_daily (
    id INT AUTO_INCREMENT PRIMARY KEY,
    created DATETIME NOT NULL,
    recap LONGTEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS recaps_daily_by_created ON recaps_daily(created);

CREATE TABLE IF NOT EXISTS recaps_weekly (
    id INT AUTO_INCREMENT PRIMARY KEY,
    created DATETIME NOT NULL,
    recap LONGTEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS recaps_weekly_by_created ON recaps_weekly(created);