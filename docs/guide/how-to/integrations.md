# Connect Graylog and Unimus

Both are optional. Your phone talks to each one directly, with its own sign-in, so it must be able to reach them.

## Connect Graylog

1. Open **Settings → Graylog** and turn on **Enable Graylog**.
2. Enter the server address, and the port if it isn't the usual one. Change **Version** only for an old or customised server.
3. Enter a login. To use an access token, put the token in the user name and `token` as the password.
4. Set the time zone if message times look wrong.
5. Choose **Test connection**. It checks what you've entered without saving it.
6. Choose **Save**.

Messages are in **More → Graylog** and on each device's page. If a device's messages are missing, turn on **Match any address** to look for every address on its interfaces.

## Connect Unimus

1. Open **Settings → Unimus** and turn on **Enable Unimus**.
2. Enter the server address and an API token.
3. If LibreNMS adds a domain to short names (its `mydomain` setting), enter it under **Matching devices**, for example `example.net`.
4. Choose **Test connection**, then **Save**.

Devices are matched to Unimus by host name, then by address.

## Read and compare config backups

1. Open a device and tap **Config backups**.
2. Tap a backup to read or search it.
3. To see what changed, choose **Compare**, tick two backups, then choose **Compare** again.

Either can be shared. Check what you share: a configuration can hold sensitive details. Taking a new backup is done in Unimus or in DashyNMS on a computer.

## Remove a saved password

**Forget saved password** and **Forget saved token** remove it from this phone straight away, after you confirm.
