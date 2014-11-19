NLog.Targets.ElasticSearch
==========================

```xml
<targets>
  <target xsi:type="BufferingWrapper" flushTimeout="5000">
    <target xsi:type="ElasticSearch" host="localhost" port="7200" />
  </target>
</targets>
```
