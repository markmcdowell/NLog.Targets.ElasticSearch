NLog.Targets.ElasticSearch
==========================

```xml
<targets>
  <target xsi:type="BufferingWrapper" flushTimeout="5000">
    <target xsi:type="ElasticSearch" index="logstash-${shortdate}" type="logevent" />
  </target>
</targets>
```
