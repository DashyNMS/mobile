# Schedule maintenance and rediscover devices

## Schedule maintenance

1. Open a device and choose **Maintenance**. For several devices, select them in **Devices** first.
2. Choose **Start now**, or a later date and time. Times are in the LibreNMS server's own time.
3. Choose how long it lasts.
4. Under **Alerts during maintenance**, choose whether alerts are skipped, muted or still run.
5. Add a title and notes if you like, then choose **Schedule**.

Back discards the form without scheduling anything. If some devices were refused, DashyNMS says which, and the others are still scheduled. Scheduling needs permission on your LibreNMS account.

## Rediscover a device

Choose **Rediscover** on a device, or swipe it in the list, after changing its hardware or ports. LibreNMS runs discovery when it's next able to, so the change may take several minutes to show.
