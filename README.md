# RetroArch-AI-with-IoTEdge
Using [IoTEdge](https://docs.microsoft.com/en-us/azure/iot-edge/?wt.mc_id=RetroArchAIwithIoTEdge-github-pdecarlo) with [Cognitive Services Containers](https://docs.microsoft.com/en-us/azure/cognitive-services/cognitive-services-container-support?wt.mc_id=RetroArchAIwithIoTEdge-github-pdecarlo) to enhance Retro Video Games 🧠+🎮

This project uses AI services running side-by-side [Retroarch](https://www.retroarch.com/) on top of [Lakka](www.lakka.tv/
) through the use of containers to allow for interesting interactions with Retroarch in a modular and remotely configurable fashion.

## Current Modules

**ScreenshotTranslator**

[![Docker Pulls](https://img.shields.io/docker/pulls/toolboc/retroarch-screenshot-translator.svg?style=flat-square)](https://hub.docker.com/r/toolboc/retroarch-screenshot-translator)

<img src="http://i.imgur.com/uYzisSO.jpg" width="600" />

![Demo](https://i.imgur.com/sXnbjOi.gif)

*Translates screenshots captured in /storage/screenshots/ for display to the framebuffer of fb0 using a local container instance of [cognitive-services-recognize-text](https://docs.microsoft.com/en-us/azure/cognitive-services/computer-vision/computer-vision-how-to-install-containers#docker-pull-for-the-recognize-text-container)  and a custom ScreenshotTranlator module for IoT Edge*

| Parameter      | Description |   Type        |
| -------------- | ------------| --------- |
| Fontsize      | Controls the size of the rendered translation font (default: 25) | Integer  |
| FontFamily   | Controls the FontFamily used to render the translation (default: DejaVuSansMono-Bold)   | string |
| Language   | Controls the Language to translate text to (currently only translates from English)   | string |

Ex: To translate to Japanese and display japanese characters, set the desired properties for the module twin of `ScreenshotTranslator` on the device in question for `FontFamily` to "TakaoPMincho" and `Language` to "ja".
  
<img src="http://i.imgur.com/lIo70jm.png" width="600" />
<img src="http://i.imgur.com/u3haWXz.jpg" width="600" />

FontFamily values are obtained from installed fonts in `/usr/share/fonts/truetype/*`

Valid values include:
* DejaVuSans-Bold 
* DejaVuSans  
* DejaVuSansMono-Bold  
* DejaVuSansMono  
* DejaVuSerif-Bold
* DejaVuSerif
* TakaoMincho (Japanese)
* TakaoPMincho (Japanese) 
* NanumGothicBold (Korean)
* NanumMyeongjoBold (Korean)

Language values can be obtained from the [Microsoft Text Translator Language Support Documentation](https://docs.microsoft.com/en-us/azure/cognitive-services/translator/language-support?wt.mc_id=RetroArchAIwithIoTEdge-github-pdecarlo)


# Installation and Setup

Requires an x64 compatible device with a screen and HDMI out and access to the [Cogntive Services Computer Vision Containers Preview](https://docs.microsoft.com/en-us/azure/cognitive-services/computer-vision/computer-vision-how-to-install-containers#request-access-to-the-private-container-registry?wt.mc_id=RetroArchAIwithIoTEdge-github-pdecarlo).

1 - Download the [latest Lakka.img.gz release from this fork of Lakka-LibreELEC](https://github.com/toolboc/Lakka-LibreELEC/releases/download/RetroArch-AI-with-IoTEdge_v1/Lakka-Generic.x86_64-2.2-RetroArch-AI-with-IoTEdge.img.gz) and install it by following these [instructions](http://www.lakka.tv/get/windows/generic/install/).

2 - After installation, configure the device to output the RetroArch UI over HDMI by select the appropriate monitor index in [Video Settings](http://www.lakka.tv/doc/Video-settings/), you may also want to configure audio output to route over HDMI as well by configuring the appropriate Audio Device in [Audio Settings](http://www.lakka.tv/doc/Audio-settings/).

3 - Configure the Wifi settings to connect to an approprite access point in the [WiFi-Configuration Interface](http://www.lakka.tv/articles/2016/10/06/major-release-brings-wifi-and-simplified-interface/#wi-fi-configuration-interface)

4 - SSH into your Lakka Device by following these [instructions](http://www.lakka.tv/doc/Accessing-Lakka-command-line-interface/) and execute the following commands to install the Docker Add-On:
```
cd ~/.kodi/

wget https://github.com/toolboc/Lakka-LibreELEC/releases/download/RetroArch-AI-with-IoTEdge_v1/service.system.docker-8.2.122.zip

unzip service.system.docker-8.2.122.zip

cd ~/.kodi/addons/service.system.docker/system.d/

systemctl enable service.system.docker.service

reboot
```
Note: Docker will install to the storage partition and is accessible via the path:
```
/storage/.kodi/addons/service.system.docker/bin/docker
```

Verify that docker is running with:
```
export PATH=/bin:/sbin:/usr/bin:/usr/sbin:/storage/.kodi/addons/service.system.docker/bin
docker ps
```

5 - Create an Azure IoT Hub by following these [instructions](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-create-through-portal?wt.mc_id=RetroArchAIwithIoTEdge-github-pdecarlo
) then [create a new IoT Edge Device in the Azure Portal](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-register-device-portal?wt.mc_id=RetroArchAIwithIoTEdge-github-pdecarlo) and retrieve the connection string.

6 - SSH into your Lakka Device by following these [instructions](http://www.lakka.tv/doc/Accessing-Lakka-command-line-interface/) and verify that docker is running with:
```
export PATH=/bin:/sbin:/usr/bin:/usr/sbin:/storage/.kodi/addons/service.system.docker/bin
docker ps
```
Create an instance of an [azure-iot-edge-device-container](https://github.com/toolboc/azure-iot-edge-device-container) with (be sure to replace <IoTHubDeviceConnectionString> with the key obtained in Step 5):

```
docker run --name edge-device-container --restart always -d --privileged -v /storage/screenshots:/storage/screenshots -v /dev/fb0:/dev/fb0 -e connectionString='<IoTHubDeviceConnectionString>' toolboc/azure-iot-edge-device-container
```

7 - Install [Visual Studio Code](https://code.visualstudio.com/) onto an available development machine and install the [Azure IoT Edge extension for Visual Studio Code](https://marketplace.visualstudio.com/items?itemName=vsciot-vscode.azure-iot-edge?wt.mc_id=RetroArchAIwithIoTEdge-github-pdecarlo).  

8 - Clone or download a copy of [this repo](https://github.com/toolboc/RetroArch-AI-with-IoTEdge) and open the `RetroArch-AI-with-IoTEdge` folder in Visual Studio Code.  Next, press `F1` and select `Azure IoT Hub: Select IoT Hub` and choose the IoT Hub you created in Step 5, follow the prompts to complete the process.

9 - In VS Code, navigate to the `deployment.template.json` file and modify the following values:

* `{YourPrivateRepoUsername}` - The username provided for access to the [Cogntive Services Computer Vision Containers Preview](https://docs.microsoft.com/en-us/azure/cognitive-services/computer-vision/computer-vision-how-to-install-containers#request-access-to-the-private-container-registry?wt.mc_id=RetroArchAIwithIoTEdge-github-pdecarlo).
* `{YourPrivateRepoPassword}` - The password provided for access to the [Cogntive Services Computer Vision Containers Preview](https://docs.microsoft.com/en-us/azure/cognitive-services/computer-vision/computer-vision-how-to-install-containers#request-access-to-the-private-container-registry?wt.mc_id=RetroArchAIwithIoTEdge-github-pdecarlo).
* `{YourCognitiveServicesApiKey}` - Follow these instruction to [create a Cognitive Services Resource in Azure](https://docs.microsoft.com/en-us/azure/cognitive-services/cognitive-services-apis-create-account?wt.mc_id=RetroArchAIwithIoTEdge-github-pdecarlo), obtain and replace with the respective key.
* `{YourBillingEndpointLocale}` - The locale prefix used for the endpoint of your Cognitive Services Resource
* `{YourTranslatorTextApiKey}` - Follow these instruction to [create a Cognitive Services Translator Text API Resource in Azure](https://docs.microsoft.com/en-us/azure/cognitive-services/translator/translator-text-how-to-signup?wt.mc_id=RetroArchAIwithIoTEdge-github-pdecarlo), obtain and replace with the respective key.

10 - Create a deployment for the IoT Edge device by right-clicking `deployment.template.json` and select `Generate IoT Edge Deployment Manifest`.  This will create a file under the config folder named `deployment.amd64.json`, right-click that file and select `Create Deployment for Single Device` and select the device you created in Step 5.

Wait a few minutes for the deployment to complete and you should see the logo for the RetroArch Screenshot Translator appear on the main screen of your Lakka machine.

Configure a Hotkey or manually select "Take Screenshot" while playing a game, the translated image is stored to `/storage/screenshots/translated/` and displayed on the main screen of the Lakka Device.
