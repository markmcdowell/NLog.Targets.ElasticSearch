NLog.Targets.ElasticSearch
==========================

The ElasticSearch target works best with the BufferingWrapper target applied. By default the target assumes an ElasticSearch node is running on the localhost on port 9200.

```xml
<nlog>
  <extensions>
    <add assembly="NLog.Targets.ElasticSearch"/>
  </extensions>
  <targets>
    <target name="elastic" xsi:type="BufferingWrapper" flushTimeout="5000">
  	  <target xsi:type="ElasticSearch" index="logstash-${shortdate}"/>
    </target>
  </targets>
  <rules>
    <logger name="*" minlevel="Info" writeTo="elastic" />
  </rules>
</nlog>
```
