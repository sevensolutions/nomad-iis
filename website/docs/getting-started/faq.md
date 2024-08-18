---
sidebar_position: 99
---

# 💡 Good to know / FAQ

## Anonymous Authentication and the IUSR account{#iusr-account}

By default, this driver will permit the built-in [*IUSR-account*](https://learn.microsoft.com/en-us/iis/get-started/planning-for-security/understanding-built-in-user-and-group-accounts-in-iis#understanding-the-new-iusr-account) to the *local* task directory.
This should allow anonymous authentication to work directly.  
You can optionally disable this by setting `permit_iusr = false`.
In this case you may need to add the following snippet to your *web.config* to make anonymous authentication use the *AppPoolIdentity* instead.

```xml
<configuration>
  <system.webServer>
    <security>
      <authentication>
        <anonymousAuthentication userName="" />
      </authentication>
    </security>
  </system.webServer>
</configuration>
```

:::important
By default, changing the anonymous authentication via custom web.config is not allowed in IIS and you will get a *500 - Internal Server Error*.
The corresponding section is locked on IIS Instance level.
To unlock it, open the IIS Management Console, select the Server node on the left side and then navigate to *Feature Delegation*. Look for the entry *Authentication - Anonymous* and change it to *Read/Write*.
If you want to automate this process, run the following Powershell Command:
`Set-WebConfiguration //System.WebServer/Security/Authentication/anonymousAuthentication -metadata overrideMode -value Allow -PSPath IIS:/`
```

## asp-net-sample-app returns 500 - Internal Server Error

The asp-net-sample-app changes the anonymous authentication in a way, so that the App Pool Identity is being used.  
Please see the *Important*-box above.
