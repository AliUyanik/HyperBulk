# HyperBulk
RabbitMQ Bulk Consumer with Polly Retry mechanism and RabbitMQ deadletter

# Usage
Creaye your class inherited from BulkConsumer <br>
Override Received method <br>
In this method you can do whatever you want to do. Api call or start another process. <br>
Do not use try catch inside the method. because polly will take care of it for you.

![usage](https://user-images.githubusercontent.com/3394985/205980428-87784e54-a552-439a-9856-9d8d192215db.jpg)

# ConsumerSettings

AppSettings.ConsumeDelay          => You say that the application should process the data it collects at intervals of how many seconds.<br><br>
AppSettings.ReconnectCount        => If the app crashes, how many times should it try to run it again? if you set it to 0 it will try infinite times. If you enter a certain number, it will automatically close the application when it reaches that number. <br><br>
AppSettings.ReconnectDelay        => you tell the application how long (in seconds) it should wait for it to run again. <br><br>

RabbitMQSettings                  => Check rabbitmq offical website <br><br>
RabbitMQSettings.PrefetchOrigins  => In cases where the number of queues is greater than "Key" in the dictionary object you sent, you replace the prefetch count property with the "Value" value. So you balance the consume state.

RetrySettings.Count               => Specifies how many times to try again if the method you override gets an error. <br><br>
RetrySettings.Delay               => The time (in seconds) between each new attempt is given when the method receives an error. performs the next attempt by multiplying the number of errors by the given second.
