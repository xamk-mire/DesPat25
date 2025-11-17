#include <WiFi.h>
#include <PubSubClient.h>

const char* ssid        = "ssid"; // Change to match your network ssid
const char* password    = "pw"; // Change to match your network password

// MQTT broker (your computer IP)
const char* mqtt_server = "10.21.0.147";   // change to your PC's IP
const int   mqtt_port   = 1883;

const char* deviceName = "GreenhousePi";    // must match DeviceName in backend
const char* sensorType = "SoilMoisture";    // will map to SensorTypeEnum.SoilMoisture

WiFiClient espClient;
PubSubClient client(espClient);

const int MOISTURE_PIN = 34;

// Calibrate these
int wet = 1500;   
int dry = 3600;

unsigned long lastPublish = 0;
const unsigned long PUBLISH_INTERVAL = 10000; // ms

void setup_wifi() {
  delay(10);
  Serial.println();
  Serial.print("Connecting to ");
  Serial.println(ssid);

  WiFi.begin(ssid, password);

  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }

  Serial.println("");
  Serial.println("WiFi connected");
  Serial.print("IP address: ");
  Serial.println(WiFi.localIP());
}

void reconnect() {
  // Loop until reconnected
  while (!client.connected()) {
    Serial.print("Attempting MQTT connection...");
    // Create a client ID
    String clientId = "ESP32-Moisture-";
    clientId += String((uint32_t)ESP.getEfuseMac(), HEX);

    // Attempt to connect (no user/pass for now)
    if (client.connect(clientId.c_str())) {
      Serial.println("connected");
      // If you needed to subscribe to commands, you'd do it here
      // client.subscribe("sensors/plant1/cmd");
    } else {
      Serial.print("failed, rc=");
      Serial.print(client.state());
      Serial.println(" try again in 5 seconds");
      delay(5000);
    }
  }
}

void setup() {
  Serial.begin(115200);
  analogReadResolution(12);

  setup_wifi();
  client.setServer(mqtt_server, mqtt_port);
}

void loop() {
  if (!client.connected()) {
    reconnect();
  }
  client.loop();

  unsigned long now = millis();
  if (now - lastPublish > PUBLISH_INTERVAL) {
    lastPublish = now;

    int raw = analogRead(MOISTURE_PIN);

    float moisture = 100.0 * (dry - raw) / (dry - wet);
    moisture = constrain(moisture, 0, 100);

    Serial.print("Raw: ");
    Serial.print(raw);
    Serial.print("  Moisture %: ");
    Serial.println(moisture);

    // Build topic: greenhouse/{deviceName}/sensor/{sensorType}
    String topic = "greenhouse/";
    topic += deviceName;
    topic += "/sensor/";
    topic += sensorType;

    // Build JSON payload that backend expects
    String payload = "{";
    payload += "\"value\":";
    payload += moisture;
    payload += ",\"unit\":\"%\"";
    payload += ",\"raw\":";
    payload += raw;
    payload += "}";

    bool ok = client.publish(topic.c_str(), payload.c_str());
    if (ok) {
      Serial.print("MQTT publish ok to topic: ");
      Serial.println(topic);
    } else {
      Serial.println("MQTT publish failed");
    }
  }
}

