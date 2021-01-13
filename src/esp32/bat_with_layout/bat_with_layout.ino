/*
    Based on Neil Kolban example for IDF: https://github.com/nkolban/esp32-snippets/blob/master/cpp_utils/tests/BLE%20Tests/SampleServer.cpp
    Ported to Arduino ESP32 by Evandro Copercini
    updates by chegewara
*/

#include <BLEDevice.h>
#include <BLEUtils.h>
#include <BLEServer.h>
#include <lvgl.h>
#include <SPI.h>
#include <TFT_eSPI.h>       // Hardware-specific library

#include "esp_bt_main.h"
#include "esp_bt_device.h"

#include "mute.h"
#include "bat.h"
#include "bluetooth.h"
#include "wave.h"

TFT_eSPI tft = TFT_eSPI();  // Invoke custom library
static lv_disp_buf_t disp_buf;
static lv_color_t buf[LV_HOR_RES_MAX * 10];

// See the following for generating UUIDs:
// https://www.uuidgenerator.net/

#define SERVICE_UUID        "4dd08908-637c-4d4e-95ca-90b84fe2fcea"
#define MOUSE_UUID          "8f43ee07-60d0-4f43-a350-39c8fdf6dc5d"
#define HEADSET_UUID        "e54807c3-f811-4a63-8c0a-983f7ccea869"
#define MUTE_UUID           "5170b989-5e82-49c7-8000-15f88f9ff17e"
#define VOLUME_UUID         "2f0a825a-b3d2-49d0-a2a1-884e1cf8b975"



struct MouseHEADSET {
  uint32_t mouse;
  uint32_t headset;
  uint32_t mute;
  uint32_t volume;
};

BLECharacteristic *mouseCharacteristic;
BLECharacteristic *headsetCharacteristic;
BLECharacteristic *muteCharacteristic;
BLECharacteristic *volCharacteristic;
MouseHEADSET data;

bool redraw=false;

#if USE_LV_LOG != 0
/* Serial debugging */
void my_print(lv_log_level_t level, const char * file, uint32_t line, const char * dsc)
{

    Serial.printf("%s@%d->%s\r\n", file, line, dsc);
    Serial.flush();
}
#endif

/* Display flushing */
void my_disp_flush(lv_disp_drv_t *disp, const lv_area_t *area, lv_color_t *color_p)
{
    uint32_t w = (area->x2 - area->x1 + 1);
    uint32_t h = (area->y2 - area->y1 + 1);

    tft.startWrite();
    tft.setAddrWindow(area->x1, area->y1, w, h);
    tft.pushColors(&color_p->full, w * h, true);
    tft.endWrite();

    lv_disp_flush_ready(disp);
}

class MyServerCallbacks: public BLEServerCallbacks {
    void onConnect(BLEServer* pServer) {
      Serial.println("onConnect");
      BLEDevice::startAdvertising();
      redraw = true;
    };

    void onDisconnect(BLEServer* pServer) {
      Serial.println("onDisconnect");
      BLEDevice::startAdvertising();
      redraw = true;
    }
};

void printDeviceAddress() {
 
  const uint8_t* point = esp_bt_dev_get_address();
 
  for (int i = 0; i < 6; i++) {
 
    char str[3];
 
    sprintf(str, "%02X", (int)point[i]);
    Serial.print(str);
 
    if (i < 5){
      Serial.print(":");
    }
 
  }
}

lv_obj_t * connectionText;
lv_obj_t * mouseBatLevelText;
lv_obj_t * headsetBatLevelText;
//lv_obj_t * muteText;
lv_obj_t * progressMouseBat;
lv_obj_t * progressHeadsetBat;
lv_obj_t * progressHeadsetVolume;
lv_obj_t * headsetVolText;
BLEServer *pServer;
lv_obj_t * muteIcon;
lv_obj_t * headsetIcon;
lv_obj_t * mouseIcon;
lv_obj_t * headsetVolIcon;
lv_obj_t * connectionIcon;
lv_img_dsc_t mute;
lv_img_dsc_t bat;
lv_img_dsc_t wave;
lv_img_dsc_t bluetooth;
  
TaskHandle_t Task1;

void Task1code( void * parameter) {
  for(;;) {
    int mouse = atoi(mouseCharacteristic->getValue().c_str());
    int headset = atoi(headsetCharacteristic->getValue().c_str());
    int mute = atoi(muteCharacteristic->getValue().c_str());
    int volume = atoi(volCharacteristic->getValue().c_str());
    
    if(mouse != data.mouse || headset != data.headset || redraw || data.mute != mute || volume != data.volume)
    {
      Serial.printf("1");
      redraw = false;
      data.mouse = mouse;
      data.headset = headset;
      data.mute = mute;
      data.volume = volume;
  
      Serial.printf("2");
      if(data.mouse == 9999)
      {
        lv_label_set_text(mouseBatLevelText, "Carregando!");
        lv_bar_set_value(progressMouseBat, 100, LV_ANIM_ON);
      }
      else if(data.headset <= 100)
      {
        lv_label_set_text_fmt(mouseBatLevelText, "%d%%", data.mouse);
        lv_bar_set_value(progressMouseBat, data.mouse, LV_ANIM_ON);
      }
      else
      {
        lv_label_set_text(mouseBatLevelText, "Erro...");
        lv_bar_set_value(progressMouseBat, 0, LV_ANIM_ON);
      }
      lv_obj_align(mouseBatLevelText, NULL, LV_ALIGN_CENTER, 0, 0);
      lv_label_set_align(mouseBatLevelText, LV_LABEL_ALIGN_CENTER);    
      Serial.printf("3");

      if(data.headset == 9999)
      {
        lv_label_set_text(headsetBatLevelText, "Carregando!");
        lv_bar_set_value(progressHeadsetBat, 100, LV_ANIM_ON);
      }
      else if(data.headset <= 100)
      {
        lv_label_set_text_fmt(headsetBatLevelText, "%d%%", data.headset);
        lv_bar_set_value(progressHeadsetBat, data.headset, LV_ANIM_ON);
      }
      else
      {
        lv_label_set_text(headsetBatLevelText, "Erro...");
        lv_bar_set_value(progressHeadsetBat, 0, LV_ANIM_ON);
      }
      lv_obj_align(headsetBatLevelText, NULL, LV_ALIGN_CENTER, 0, 0);
      lv_label_set_align(headsetBatLevelText, LV_LABEL_ALIGN_CENTER);    

      Serial.printf("4");
  
      lv_label_set_text_fmt(headsetVolText, "%d%%", data.volume);
      lv_bar_set_value(progressHeadsetVolume, data.volume, LV_ANIM_ON);
      lv_obj_align(headsetVolText, NULL, LV_ALIGN_CENTER, 0, 0);
      lv_label_set_align(headsetVolText, LV_LABEL_ALIGN_CENTER);    

      Serial.printf("5");
      
      if(data.mute == 1)
      {
        //lv_label_set_text(muteText, "Mudo");
        lv_obj_set_hidden(muteIcon, false);
        
      }
      else
      {
        //lv_label_set_text(muteText, "");
        lv_obj_set_hidden(muteIcon, true);
      }
      if(pServer->getConnectedCount() > 0)
      {
        lv_obj_set_hidden(connectionIcon, false);
        
        /*lv_label_set_text(connectionText, "Conectado");
        lv_obj_set_style_local_text_color(connectionText, LV_LABEL_PART_MAIN, LV_STATE_DEFAULT, LV_COLOR_ORANGE );
        lv_obj_align(connectionText, NULL, LV_ALIGN_CENTER, 0, 0);*/
      }
      else
      {
        lv_obj_set_hidden(connectionIcon, true);

        /*lv_label_set_text(connectionText, "Desconectado");
        lv_obj_set_style_local_text_color(connectionText, LV_LABEL_PART_MAIN, LV_STATE_DEFAULT, LV_COLOR_BLACK );
        lv_obj_align(connectionText, NULL, LV_ALIGN_CENTER, 0, 0);*/
      }
            Serial.printf("6");

    }

    delay(5);
  }
}

void setup() {
  Serial.begin(115200);
  Serial.println("Controle de Bateria");

  lv_init();

  #if USE_LV_LOG != 0
    lv_log_register_print_cb(my_print); /* register print function for debugging */
  #endif


  //tft.init();
  tft.begin();
  lv_disp_buf_init(&disp_buf, buf, NULL, LV_HOR_RES_MAX * 10);  

  /*Initialize the display*/
  lv_disp_drv_t disp_drv;
  lv_disp_drv_init(&disp_drv);
  disp_drv.hor_res = 240;
  disp_drv.ver_res = 240;
  disp_drv.flush_cb = my_disp_flush;
  disp_drv.buffer = &disp_buf;
  lv_disp_drv_register(&disp_drv);

  mute.header.always_zero = 0;
  mute.header.w = 64;
  mute.header.h = 64;
  mute.data_size = 4096 * LV_COLOR_SIZE / 8;
  mute.header.cf = LV_IMG_CF_TRUE_COLOR;
  mute.data = mute_map;
  
  bat.header.always_zero = 0;
  bat.header.w = 19;
  bat.header.h = 35;
  bat.data_size = 665 * LV_COLOR_SIZE / 8;
  bat.header.cf = LV_IMG_CF_TRUE_COLOR;
  bat.data = bat_map;
 
  wave.header.always_zero = 0;
  wave.header.w = 21;
  wave.header.h = 35;
  wave.data_size = 735 * LV_COLOR_SIZE / 8;
  wave.header.cf = LV_IMG_CF_TRUE_COLOR;
  wave.data = wave_map;

  bluetooth.header.always_zero = 0;
  bluetooth.header.w = 30;
  bluetooth.header.h = 30;
  bluetooth.data_size = 900 * LV_COLOR_SIZE / 8;
  bluetooth.header.cf = LV_IMG_CF_TRUE_COLOR;
  bluetooth.data = bluetooth_map;
    
  lv_obj_t * containerMouse = lv_cont_create(lv_scr_act(), NULL);
  lv_obj_set_x(containerMouse, 5);
  lv_obj_set_y(containerMouse, 5);
  lv_obj_set_width(containerMouse, 230);
  lv_obj_set_height(containerMouse, 70);
  
  lv_obj_t * labelMouse = lv_label_create(containerMouse, NULL);
  lv_obj_set_x(labelMouse, 5);
  lv_obj_set_y(labelMouse, 5);
  lv_obj_set_width(labelMouse, 220);
  lv_label_set_static_text(labelMouse, "Mouse");

  mouseIcon = lv_img_create(containerMouse, NULL);
  lv_img_set_src(mouseIcon, &bat);
  lv_obj_set_x(mouseIcon, 12);
  lv_obj_set_y(mouseIcon, 27);
  
  progressMouseBat = lv_bar_create(containerMouse, NULL);
  lv_obj_set_x(progressMouseBat, 36);
  lv_obj_set_y(progressMouseBat, 27);
  lv_obj_set_width(progressMouseBat, 130);
  lv_obj_set_height(progressMouseBat, 33);
  lv_obj_set_height(containerMouse, 70);
  
  mouseBatLevelText = lv_label_create(progressMouseBat, NULL);
  lv_label_set_text(mouseBatLevelText, "Desconhecido");
  lv_obj_align(mouseBatLevelText, NULL, LV_ALIGN_CENTER, 0, 0);
  lv_label_set_align(mouseBatLevelText, LV_LABEL_ALIGN_CENTER);    
  lv_obj_set_y(mouseBatLevelText, 8);
  lv_obj_t * containerHeadset = lv_cont_create(lv_scr_act(), NULL);
  lv_obj_set_x(containerHeadset, 5);
  lv_obj_set_y(containerHeadset, 80);
  lv_obj_set_width(containerHeadset, 230);
  lv_obj_set_height(containerHeadset, 110);
  
  lv_obj_t * labelHeadset = lv_label_create(containerHeadset, NULL);
  lv_obj_set_x(labelHeadset, 5);
  lv_obj_set_y(labelHeadset, 5);
  lv_label_set_static_text(labelHeadset, "Headset");

  headsetIcon = lv_img_create(containerHeadset, NULL);
  lv_img_set_src(headsetIcon, &bat);
  lv_obj_set_x(headsetIcon, 12);
  lv_obj_set_y(headsetIcon, 27);
    
  progressHeadsetBat = lv_bar_create(containerHeadset, NULL);
  lv_obj_set_x(progressHeadsetBat, 36);
  lv_obj_set_y(progressHeadsetBat, 27);
  lv_obj_set_width(progressHeadsetBat, 130);
  lv_obj_set_height(progressHeadsetBat, 33);
  
  headsetBatLevelText = lv_label_create(progressHeadsetBat, NULL);
  lv_label_set_text(headsetBatLevelText, "Desconhecido");
  lv_obj_align(headsetBatLevelText, NULL, LV_ALIGN_CENTER, 0, 0);
  lv_label_set_align(headsetBatLevelText, LV_LABEL_ALIGN_CENTER);    
  lv_obj_set_y(headsetBatLevelText, 9);
  
  muteIcon = lv_img_create(containerHeadset, NULL);
  lv_img_set_src(muteIcon, &mute);
  lv_obj_set_x(muteIcon, 166);
  lv_obj_set_y(muteIcon, 31);

  headsetVolIcon = lv_img_create(containerHeadset, NULL);
  lv_img_set_src(headsetVolIcon, &wave);
  lv_obj_set_x(headsetVolIcon, 10);
  lv_obj_set_y(headsetVolIcon, 70);

  progressHeadsetVolume = lv_bar_create(containerHeadset, NULL);
  lv_obj_set_x(progressHeadsetVolume, 36);
  lv_obj_set_y(progressHeadsetVolume, 70);
  lv_obj_set_width(progressHeadsetVolume, 130);
  lv_obj_set_height(progressHeadsetVolume, 33);
  
  headsetVolText = lv_label_create(progressHeadsetVolume, NULL);
  lv_label_set_text(headsetVolText, "Desconhecido");
  lv_obj_align(headsetVolText, NULL, LV_ALIGN_CENTER, 0, 0);
  lv_label_set_align(headsetVolText, LV_LABEL_ALIGN_CENTER);
  lv_obj_set_y(headsetVolText, 9);
  
  lv_obj_t * containerConnection = lv_cont_create(lv_scr_act(), NULL);
  lv_obj_set_x(containerConnection, 5);
  lv_obj_set_y(containerConnection, 195);
  lv_obj_set_height(containerConnection, 40);
  lv_obj_set_width(containerConnection, 230);
  
  /*connectionText = lv_label_create(containerConnection, NULL);
  //lv_obj_set_x(connectionText, 5);
  lv_label_set_text(connectionText, "Desconectado");
  lv_obj_align(connectionText, NULL, LV_ALIGN_CENTER, 0, 0);
  lv_label_set_align(connectionText, LV_LABEL_ALIGN_CENTER);
  lv_obj_set_y(connectionText, 11);
  lv_obj_set_height(connectionText, 210);*/
  
  connectionIcon = lv_img_create(containerConnection, NULL);
  lv_img_set_src(connectionIcon, &bluetooth);
  lv_obj_align(connectionIcon, NULL, LV_ALIGN_CENTER, 0, 0);
  lv_obj_set_hidden(connectionIcon, true);
  
  BLEDevice::init("Controle de Bateria");
  pServer = BLEDevice::createServer();
  pServer->setCallbacks(new MyServerCallbacks());
    
  BLEService *pService = pServer->createService(SERVICE_UUID);
  
  mouseCharacteristic = pService->createCharacteristic(
                                 MOUSE_UUID,
                                 BLECharacteristic::PROPERTY_READ |
                                 BLECharacteristic::PROPERTY_WRITE
                               );

  headsetCharacteristic  = pService->createCharacteristic(
                                 HEADSET_UUID,
                                 BLECharacteristic::PROPERTY_READ |
                                 BLECharacteristic::PROPERTY_WRITE
                               );

  muteCharacteristic  = pService->createCharacteristic(
                                 MUTE_UUID,
                                 BLECharacteristic::PROPERTY_READ |
                                 BLECharacteristic::PROPERTY_WRITE
                               );

  volCharacteristic = pService->createCharacteristic(
                                 VOLUME_UUID,
                                 BLECharacteristic::PROPERTY_READ |
                                 BLECharacteristic::PROPERTY_WRITE
                               );
  data.mouse = 0;
  data.headset = 0;
  data.mute = 1;
  data.volume = 0;

  pService->start();

  // BLEAdvertising *pAdvertising = pServer->getAdvertising();  // this still is working for backward compatibility
  BLEAdvertising *pAdvertising = BLEDevice::getAdvertising();
  pAdvertising->addServiceUUID(SERVICE_UUID);
  pAdvertising->setScanResponse(true);
  pAdvertising->setMinPreferred(0x06);  // functions that help with iPhone connections issue
  pAdvertising->setMinPreferred(0x12);
  BLEDevice::startAdvertising();

  printDeviceAddress();

  xTaskCreatePinnedToCore(
      Task1code, /* Function to implement the task */
      "Task1", /* Name of the task */
      10000,  /* Stack size in words */
      NULL,  /* Task input parameter */
      0,  /* Priority of the task */
      &Task1,  /* Task handle. */
      0); /* Core where the task should run */
}

bool initalized = false;

void loop() {

  lv_task_handler(); /* let the GUI do its work */  
  
  delay(5);
}
