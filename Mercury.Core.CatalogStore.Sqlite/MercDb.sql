PRAGMA auto_vacuum = 1;

/* create a table to store name/value pairs pertaining to the store
itself. */
create table store_properties (
	name string not null primary key,
	value string not null
);

create index idx_store_properties
on store_properties(name);

create table catalog_items (
	catalog_item_id integer primary key autoincrement,
	parent_catalog_item_id integer,
	uri  text not null,
	title text not null,
	type text not null,
	/* the ID of the catalog item this is an alias to, or null if this is a primary
	catalog item */
	alias_catalog_item_id integer
);
create index idx_catalog_items_id
on catalog_items(catalog_item_id);
create index idx_catalog_items_type
on catalog_items(type);
create index idx_catalog_items_alias_id
on catalog_items(alias_catalog_item_id);
create index idx_catalog_items_parent_id
on catalog_items(parent_catalog_item_id);


/* A table to store tags, the user- or system-defined metadata
which decorate a catalog item */
create table catalog_item_tags (
	catalog_item_id integer not null,
	tag text not null,

	primary key (catalog_item_id, tag)
);

/* create an index for searching for the catalog items with a given tag */
create index idx_catalog_item_tags_tag
on catalog_item_tags(tag);

/* table to hold all words that appear in titles */
create table title_words (
	word_id integer primary key autoincrement,
	word text not null,
	one_chars text not null, /* the first character of the word */
	two_chars text null, /* the first two characters of the word */
	three_chars text null, /* the first three characters of the word */
	four_chars text null, /* the first four characters of the word */
	five_chars text null /* the first five characters of the word */
);

/* create indices on each of the five prefix columns, and the word itself */
create index idx_title_words_word on title_words(word);
create index idx_title_words_one_chars on title_words(one_chars);
create index idx_title_words_two_chars on title_words(two_chars);
create index idx_title_words_three_chars on title_words(three_chars);
create index idx_title_words_four_chars on title_words(four_chars);
create index idx_title_words_five_chars on title_words(five_chars);

/* create a table to hold the word graph, which stores the graph of words
in the catalog file names */
create table title_word_graph (
	node_id integer primary key autoincrement,
	prev_node_id integer null,
	word_id integer not null
);

/* create an index for looking up words regardless of
preceeding word */
create index idx_title_word_graph_word
on title_word_graph(word_id);

/* create an index for looking up all words that follow a given node */
create index idx_title_word_graph_pwi
on title_word_graph(prev_node_id);

/* create an index for checking if a given node follows another given word */
create index idx_title_word_graph_pwiwi
on title_word_graph(prev_node_id, word_id);

/* create an index for getting a node id given the word and prev node */
create index idx_title_word_graph_wpw
on title_word_graph(word_id, prev_node_id);

/* table to hold the list of descendant nodes for each node in the graph */
create table title_word_graph_node_dscndnts (
	node_id integer not null,
	descendant_node_id integer not null,
	primary key (node_id, descendant_node_id)
);

create index idx_title_word_graph_node_dscndnts_nid
on title_word_graph_node_dscndnts(node_id);

/* create a table to track which items' titles are included in the subtree formed by each
title word graph node and all child nodes. */
create table title_word_graph_node_items (
	node_id integer not null,
	catalog_item_id integer not null,
	primary key (node_id, catalog_item_id)
);

/* create an index for getting the catalog items included under a node */
create index idx_title_word_graph_node_items_node
on title_word_graph_node_items(node_id);

create table wtf (
	this_better_work integer primary key
);

/** triggers used to be here, but the .net wrapper around sqlite fails in odd
ways when creating triggers in a multi-command script... */

