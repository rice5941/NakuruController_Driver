#include <FlexiTimer2.h>
#include <Keyboard.h>

//int hall_din=1;
int hall_ain=A0;
uint16_t value_AD;

// 3.3V
double const inVol = 3.3;
// 3.5mm
double const stroke = 2.50;
double const threshold = 1.0;
int const keyCount = 7;

// 2.0mm
double const fixPosition = 2.0;
uint16_t const fix_AD = 440;

int count = 1;
uint16_t topDeads_AD[] = {0,0,0,0,0,0,0};
uint16_t bottomDeads_AD[] = {0,0,0,0,0,0,0};
uint16_t thresholds_AD[] = {0,0,0,0,0,0,0};

double ADToDistanceRate[] = {0,0,0,0,0,0,0};
int baseTimer = 0;

char keys[] = {'0','1','2','3','4','5','6'};
uint8_t a_keys[] = {A0,A1,A2,A3,A4,A5,A6};

// true = 押下中(pressing)
bool keys_State[] = {false,false,false,false,false,false,false};
uint8_t keys_TrueCount[] = {0,0,0,0,0,0,0};
uint8_t keys_FalseCount[] = {0,0,0,0,0,0,0};

int now = 0;
int pairID = 0;

// アナログ値送信制御
bool isAnalogOutputEnabled = false;
unsigned long lastAnalogSendTime = 0;
const unsigned long ANALOG_SEND_INTERVAL = 10; // 10ms

// ハートビート管理
unsigned long lastHeartbeatTime = 0;
const unsigned long HEARTBEAT_TIMEOUT = 10000; // 10秒

// シリアル通信バッファ
String serialBuffer = "";


void setup()
{
  //pinMode(hall_din,INPUT);
  for (int i = 0; i < keyCount; i++)
  {
    pinMode(a_keys[i],INPUT);
  }

  Keyboard.begin();
  Serial.begin(115200);

  Initialize();

  // タイマーの設定
  FlexiTimer2::set(1, SendUSB); //1ms
  //FlexiTimer2::set(1, 1.0/8000, SendUSB); //125μs
  FlexiTimer2::start();
  
  baseTimer = millis();
}

void Initialize()
{
  for (int i = 0; i < keyCount; i++)
  {
    value_AD = analogRead(a_keys[i]);
    SetTopDeadPosition(i);
  }

  delay(5000);

  for (int i = 0; i < keyCount; i++)
  {
    value_AD = analogRead(a_keys[i]);
    SetBottomDeadPosition(i);
    CalcADToDistanceRate(i);

    // 押下状態のはず.   
    /*
    keys_State[i] = true;
    Keyboard.press(keys[i]);
    */
  }
}

double CalcValueToMa(uint16_t value_AD)
{
  // AD値をmAに変換
  return ((double)value_AD * inVol) / 1024.0;
}

void SetTopDeadPosition(uint8_t i)
{
  topDeads_AD[i] = value_AD;
  Serial.print("(key:");
  Serial.print(i); 
  Serial.print(")");
  Serial.print("topDead_AD");
  Serial.print(":");
  Serial.print(value_AD);
  Serial.print("(");
  Serial.print(CalcValueToMa(value_AD));
  Serial.println("mA)");
}

void SetBottomDeadPosition(uint8_t i)
{
  bottomDeads_AD[i] = value_AD;

  Serial.print("(key:");
  Serial.print(i); 
  Serial.print(")");
  Serial.print("bottomDead_AD");
  Serial.print(":");
  Serial.print(value_AD);
  Serial.print("(");
  Serial.print(CalcValueToMa(value_AD));
  Serial.println("mA)");
}

void CalcADToDistanceRate(uint8_t i)
{
  if (topDeads_AD[i] == 0 || bottomDeads_AD[i] == 0 || topDeads_AD[i] <= bottomDeads_AD[i])
  {
    Serial.println("CalcADToDistanceRate: Failed");
    return;
  }

  ADToDistanceRate[i] = (double)(topDeads_AD[i] - bottomDeads_AD[i]) / stroke;
  thresholds_AD[i] = ((double)(bottomDeads_AD[i]) + threshold * ADToDistanceRate[i]);

  Serial.print("(key:");
  Serial.print(i); 
  Serial.print(")");
  Serial.print("ADToDistanceRate");
  Serial.print(":");
  Serial.println(ADToDistanceRate[i]);
  Serial.print("(key:");
  Serial.print(i); 
  Serial.print(")");
  Serial.print("threshold_AD");
  Serial.print(":");
  Serial.println(thresholds_AD[i]);
}

void loop()
{
  for (uint8_t i = 0; i < keyCount; i++)
  {
    value_AD = analogRead(a_keys[i]);

    if (value_AD <= thresholds_AD[i])
    {
      keys_TrueCount[i] += 1;
    }

    if (value_AD > thresholds_AD[i])
    {
      keys_FalseCount[i] += 1;
    }
  }

  // シリアルコマンド処理
  ProcessSerialCommand();

  // アナログ値の定期送信
  if (isAnalogOutputEnabled) {
    unsigned long currentTime = millis();
    if (currentTime - lastAnalogSendTime >= ANALOG_SEND_INTERVAL) {
      SendAnalogValues();
      lastAnalogSendTime = currentTime;
    }
  }

  // ハートビートタイムアウトチェック
  CheckHeartbeatTimeout();

  count += 1;

  now = millis();
  int duringTime = now - baseTimer;

  if (duringTime == 5000)
  {
    Serial.println("ScanRate:");
    Serial.print((double)(count / 5.0));
    Serial.println("Hz");
  }
} 

void OutPutValue(uint8_t i)
{
  double distance_mm = ((double)(value_AD - bottomDeads_AD[i])) / ADToDistanceRate[i];
  //printf 落ちる
  //Serial.printf("%s:(value:%s)(mm:%s)%s", count, value_AD, mm_Value);^
  Serial.print(count);
  Serial.print(":(key:");
  Serial.print(i);
  Serial.print("):(AD:");
  Serial.print(value_AD);
  Serial.print(")(mm:" );
  Serial.print(distance_mm);
  Serial.print(")(mA:" );
  Serial.print(CalcValueToMa(value_AD));
  Serial.println(")");
}

void ProcessSerialCommand()
{
  while (Serial.available()) {
    char c = Serial.read();
    if (c == '\n') {
      serialBuffer.trim();

      if (serialBuffer == "START_ANALOG") {
        isAnalogOutputEnabled = true;
        lastHeartbeatTime = millis();
        Serial.println("{\"status\":\"started\"}");
      }
      else if (serialBuffer == "STOP_ANALOG") {
        isAnalogOutputEnabled = false;
        Serial.println("{\"status\":\"stopped\"}");
      }
      else if (serialBuffer == "HEARTBEAT") {
        lastHeartbeatTime = millis();
      }

      serialBuffer = "";
    } else {
      serialBuffer += c;
    }
  }
}

void SendAnalogValues()
{
  Serial.print("{\"type\":\"analog_values\",");
  Serial.print("\"timestamp\":");
  Serial.print(millis());
  Serial.print(",\"keys\":[");

  for (int i = 0; i < keyCount; i++) {
    uint16_t ad_value = analogRead(a_keys[i]);
    bool pressed = (ad_value <= thresholds_AD[i]);

    Serial.print("{\"id\":");
    Serial.print(i);
    Serial.print(",\"ad\":");
    Serial.print(ad_value);
    Serial.print(",\"pressed\":");
    Serial.print(pressed ? "true" : "false");
    Serial.print("}");

    if (i < keyCount - 1) Serial.print(",");
  }

  Serial.println("]}");
}

void CheckHeartbeatTimeout()
{
  if (isAnalogOutputEnabled) {
    unsigned long currentTime = millis();
    if (currentTime - lastHeartbeatTime > HEARTBEAT_TIMEOUT) {
      isAnalogOutputEnabled = false;
      Serial.println("{\"status\":\"timeout\"}");
    }
  }
}

void SendUSB()
{
  for (uint8_t i = 0; i < keyCount; i++)
  {
    bool resultKeyState = keys_State[i];

    if (keys_FalseCount[i] < keys_TrueCount[i])
    {
      resultKeyState = true;
    }

    if (keys_TrueCount[i] < keys_FalseCount[i] )
    {
      resultKeyState = false;
    }

    if (resultKeyState)
    {
      if (keys_State[i] == false)
      {
        keys_State[i] = true;

        if (i == 6)
        {
          Keyboard.press(KEY_LEFT_CTRL);
        }
        else
        {
          Keyboard.press(keys[i]);
        }
      }
    }
    else
    {
      if (keys_State[i] == true)
      {
        keys_State[i] = false;

        if (i == 6)
        {
          Keyboard.release(KEY_LEFT_CTRL);
        }
        else
        {
          Keyboard.release(keys[i]);
        }
        
        pairID += 1; 
      }
    }
  }

  for (int i = 0; i < keyCount; i++)
  {
      keys_TrueCount[i] = 0;
      keys_FalseCount[i] = 0;
  }
}
      