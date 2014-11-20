NLog.Targets.ElasticSearch
==========================

The ElasticSearch target works best with the BufferingWrapper target applied. By default the target assumes an ElasticSearch node is running on the localhost on port 9200.

```xml
<targets>
  <target xsi:type="BufferingWrapper" flushTimeout="5000">
    <target xsi:type="ElasticSearch" index="logstash-${shortdate}" documentType="logevent" />
  </target>
</targets>
```
