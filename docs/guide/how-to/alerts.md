# Investigate and acknowledge alerts

Alerts lists what's open, most severe first. Acknowledging an alert tells your team you're handling it. It doesn't fix the device or change the rule.

## Find an alert

1. Open **Alerts**.
2. Tap **Critical**, **Warning**, **OK** or **Acknowledged** to choose what's listed. Each shows its count, and your choice is remembered.
3. Search by device, rule, note or alert ID.

On LibreNMS 26.10 and later, the faults a rule finds on one device appear as one group. Expand it to see each fault, such as each port, and whether only some are acknowledged.

## Work out why it fired

Tap an alert to open it:

- **Why it fired** shows the values the rule tests. **Show all fields** adds the rest.
- The rule's condition, notes and procedure follow.
- **History** shows how often it's happened before. DashyNMS says when the history is incomplete or couldn't be read.
- **Open device** takes you to the device's ports, sensors and graphs.

## Acknowledge

1. Swipe an alert in the list and choose **Acknowledge**, or use the button on the alert's page.
2. Add a note if you like, such as `Checking the uplink; next update 14:30`.

Choose **Unacknowledge** the same way. On LibreNMS 26.10 and later, acknowledging in the list applies to the whole group. **Acknowledge fault** appears on a single fault only when LibreNMS can acknowledge it without touching its siblings.

DashyNMS reads the alert back afterwards and says what changed. If something was refused, your LibreNMS account may not have permission for that device.

## Share the list

Choose **Export as CSV** to share the alerts shown. Check the file first: it names devices.

## See what a rule does

**More → Alert rules** lists each rule, what it matches and what it's alerting. Rules can be read on the phone but not changed. A rule that tests a field your LibreNMS version has removed says what to use instead.
