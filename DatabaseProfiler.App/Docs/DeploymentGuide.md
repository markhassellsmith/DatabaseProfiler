# Database Profiler App Deployment Guide

## Purpose
This document captures the deployment settings and operational notes needed to keep long-running table reports reliable in server environments.

## Table Report Reliability Goal
Table reports can take several minutes to complete. The application is designed so report generation starts quickly, returns control to the browser, and continues in the background while the UI polls for progress.

For a server deployment, the main objective is to avoid the host ending the worker process before the report finishes.

## Recommended Hosting Model
- Prefer running the app as a long-lived ASP.NET Core site.
- Avoid relying on request lifetime for report completion.
- Keep the report queue and worker running inside the app process unless a separate worker service is introduced later.

## IIS / Server Hardening Reference
The settings below are the IIS/server deployment-hardening items that should be reviewed before the app is placed into production. They are the practical controls that reduce report interruption risk.

## IIS Settings to Review
If hosting under IIS, review these settings for the application pool and site:

### Application Pool
- **.NET CLR Version**: `No Managed Code`
- **Managed Pipeline Mode**: `Integrated`
- **Start Mode**: `AlwaysRunning`
- **Idle Time-out (minutes)**: set to `0` for always-on behavior, or extend it well beyond the longest expected report
- **Regular Time Interval (minutes)** recycle: disable or extend if reports are long-running
- **Specific Times** recycle: avoid scheduled recycles during report usage windows
- **Load User Profile**: enable if the hosting environment requires it for the app identity

### Site / Application
- **Preload Enabled**: `True`
- **Application Initialization**: enable if available so the site warms up before the first user request
- Keep the application assigned to an app pool that is not shared with unrelated apps

## IIS Operational Notes
- Avoid recycles during business hours if long table reports are expected.
- Confirm that the worker process is not being stopped by idle shutdown.
- Confirm that the app pool identity has access to the database server and any required network resources.
- Ensure the server has enough memory and CPU headroom for long report generation.

## `web.config` Settings
If the app is deployed to IIS with the standard ASP.NET Core `web.config`, review these settings:

```xml
<configuration>
  <location path="" inheritInChildApplications="false">
	<system.webServer>
	  <handlers>
		<add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
	  </handlers>
	  <aspNetCore processPath="dotnet"
				  arguments=".\DatabaseProfiler.App.dll"
				  hostingModel="inprocess"
				  stdoutLogEnabled="false"
				  stdoutLogFile=".\logs\stdout" />
	</system.webServer>
  </location>
</configuration>
```

- Prefer `hostingModel="inprocess"` for best request throughput and lowest latency.
- Keep `stdoutLogEnabled="false"` during normal operation; enable it only when troubleshooting startup failures.
- Ensure the `logs` folder exists and is writable if stdout logging is enabled.
- If you ever introduce a long-running HTTP endpoint directly, consider reviewing the ASP.NET Core request timeout behavior for that endpoint separately.

## ASP.NET Core / App-Level Notes
- The report request path should remain short and should not wait for the workbook to finish building.
- The hosted background queue should be used for long-running table report execution.
- The graceful shutdown timeout should remain long enough for an in-flight report to complete cleanly when the host is stopped intentionally.
- Job state is still in-memory in the current implementation, so a process recycle will still interrupt or lose the job.

## Visual Studio Development Notes
When testing locally in Visual Studio:
- Prefer running without the debugger for timing checks.
- Use `Release` configuration when validating long-running report behavior.
- Keep in mind that stopping debugging stops the app process and will abort in-flight jobs.

## Recommended Server Hardening Checklist
1. Set the app pool to **AlwaysRunning**.
2. Disable or extend **Idle Time-out**.
3. Avoid scheduled recycles while reports are active.
4. Enable **Preload** for the site if supported.
5. Confirm the database and network connections are stable.
6. Verify the app host has enough resources for long report generation.

## Known Limitation
The current in-memory job store does not survive process restarts. If reports must survive IIS recycle or server restarts, a persistent job store or external worker service will be required.

## Summary
For the current release, the most important IIS mitigations are:
- `AlwaysRunning`
- no idle shutdown
- reduced recycle risk
- background execution for report generation

These settings reduce the chance that a long table report is interrupted before completion.
