# Shared Graylog search fixture

Synthetic messages and stream names use example.net. Tests serve Graylog's
relative search and streams endpoints over loopback HTTP, require synthetic
Basic credentials, and use Core's real Graylog client. Each adapter gets its
own connection and query log. The fixture covers current and past windows,
message order and sample metadata; existing MCP tests retain phrase, level,
stream, deep paging, zero-line and device address matching expectations.
Host tests cover permission failures and retirement. Redaction precedes line
shortening, so a cut cannot hide part of a secret pattern from the redactor.
