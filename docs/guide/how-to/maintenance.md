# Schedule maintenance and rediscover devices

## Schedule maintenance

1. Open a device and choose **Maintenance**. For several, press and hold one in **Devices**, tick the others, then choose **Maintenance** in the bar.
2. Leave **Start now** on, or turn it off and choose a date and time. Times are in the LibreNMS server's own time.
3. Choose how long it lasts.
4. Under **Alerts during maintenance**, choose **Skip alerts**, **Mute alerts** or **Run alerts as normal**.
5. Add a title and notes if you like, then choose **Schedule**. On iOS, the form scrolls above the button.

Back discards the form without scheduling anything. If some devices were refused, DashyNMS says which, and the others are still scheduled. Scheduling needs permission on your LibreNMS account.

## Rediscover a device

After changing a device's hardware or ports, open it and choose **Rediscover**. For several devices, press and hold one in the list, tick the others and choose **Rediscover**. LibreNMS runs discovery when it's next able to, so the change may take several minutes to show.
