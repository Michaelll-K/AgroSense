# AgroSense – Inteligentny system monitorowania i nawadniania upraw

## Opis projektu
AgroSense to innowacyjny system IoT dedykowany rolnikom, którzy chcą monitorować warunki upraw (takie jak wilgotność gleby, temperatura oraz natężenie światła) oraz zdalnie sterować systemem nawadniania. Dzięki integracji symulatora urządzenia IoT z chmurą Azure, AgroSense umożliwia bieżącą analizę danych, wysyłanie alertów oraz podejmowanie decyzji w czasie rzeczywistym, co przekłada się na optymalizację zużycia wody i poprawę plonów.

## Spis treści
- [Opis projektu](#opis-projektu)
- [Instalacja](#instalacja)
- [Uruchomienie projektu](#uruchomienie-projektu)
- [Zarys biznesowy](#zarys-biznesowy)
- [User Stories](#user-stories)

## Instalacja

### Wymagania wstępne
- [Docker](https://www.docker.com/get-started) oraz Docker Compose
- [Azure CLI](https://docs.microsoft.com/pl-pl/cli/azure/install-azure-cli)
- Python 3 (do uruchomienia symulatora urządzenia IoT)
- Konto w Azure z uprawnieniami do tworzenia zasobów

### Kroki instalacji

1. **Klonowanie repozytorium:**
   ```bash
   git clone https://github.com/StillNotWorth/AgroSense.git
   cd AgroSense

2. Konfiguracja Docker Compose:
Utwórz plik docker-compose.yml z poniższą zawartością:

version: "3.8"

services:
  mqtt-broker:
    image: eclipse-mosquitto:latest
    container_name: mqtt-broker
    ports:
      - "1883:1883"
    volumes:
      - ./mosquitto.conf:/mosquitto/config/mosquitto.conf


3. Konfiguracja pliku mosquitto.conf:
Utwórz plik mosquitto.conf z zawartością:

allow_anonymous true
listener 1883


4. Konfiguracja Azure IoT Hub:
Ustaw zmienne środowiskowe w terminalu:

RESOURCE_GROUP="myIotHubResourceGroup"
LOCATION="uksouth"
IOT_HUB_NAME="my-super-iot-hub"
IOT_DEVICE_NAME="my-new-device"



Zaloguj się do Azure CLI:

az login



Następnie utwórz grupę zasobów:

az group create --name $RESOURCE_GROUP --location $LOCATION




Utwórz IoT Hub:

az iot hub create --resource-group $RESOURCE_GROUP --name $IOT_HUB_NAME --location $LOCATION



Dodaj urządzenie do IoT Hub:

az iot hub device-identity create --hub-name $IOT_HUB_NAME --device-id $IOT_DEVICE_NAME


5. Instalacja symulatora urządzenia IoT:
Przejdź do katalogu symulatora (np. device_simulator) i zainstaluj wymagane pakiety:

pip install -r requirements.txt





Uruchomienie projektu

1. Uruchomienie brokera MQTT:
W katalogu głównym projektu uruchom:

docker compose up

Upewnij się, że port 1883 jest otwarty i poprawnie mapowany.


2. Uruchomienie symulatora urządzenia IoT:
Skonfiguruj plik simple_mqtt_device_simulator.py, aby łączył się z brokerem MQTT na maszynie wirtualnej (wprowadź publiczny adres IP maszyny). Następnie uruchom symulator:

python simple_mqtt_device_simulator.py

3. Integracja z Azure IoT Hub:
Po uruchomieniu symulatora urządzenia, dane będą przesyłane do brokera MQTT. Dzięki konfiguracji IoT Hub, dane te mogą być zbierane, analizowane i wykorzystywane do wysyłania alertów lub sterowania systemem nawadniania.


ZARYS BIZNESOWY

Cel projektu:
AgroSense ma na celu zwiększenie efektywności zarządzania nawadnianiem upraw poprzez monitorowanie warunków środowiskowych w czasie rzeczywistym. System pozwala na optymalizację zużycia wody, zmniejszenie kosztów oraz poprawę jakości plonów dzięki szybkiej reakcji na zmieniające się warunki pogodowe.

Korzyści dla rolników:

Obniżenie kosztów związanych z nadmiernym zużyciem wody.
Szybsze reagowanie na nagłe spadki wilgotności gleby.
Dostęp do historycznych danych, które wspierają podejmowanie lepszych decyzji dotyczących nawadniania i planowania upraw.
User Stories
JAKO rolnik
CHCIAŁBYM monitorować wilgotność gleby w czasie rzeczywistym
PO TO, ABY optymalizować proces nawadniania moich upraw.

JAKO administrator systemu
CHCIAŁBYM otrzymywać alerty, gdy wilgotność gleby spada poniżej ustalonego poziomu
PO TO, ABY szybko reagować i zapobiegać uszkodzeniom upraw.

JAKO właściciel gospodarstwa
CHCIAŁBYM mieć dostęp do raportów z danymi historycznymi
PO TO, ABY planować efektywnie przyszłe działania inwestycyjne i operacyjne.



USECASE 

Use Case: Zarządzanie Nawadnianiem na Podstawie Danych o Wilgotności Gleby

Cel:
Zapewnienie optymalnego nawadniania upraw poprzez automatyczne monitorowanie wilgotności gleby oraz umożliwienie interwencji rolnika w celu aktywacji systemu nawadniania, gdy poziom wilgotności spada poniżej ustalonego progu.

Główny aktor:

    Rolnik

System:

    AgroSense – system monitorowania warunków upraw i sterowania nawadnianiem

Warunki wstępne (Preconditions):

    System AgroSense jest zainstalowany i skonfigurowany na polu uprawnym.
    Czujniki (wilgotności, temperatury, natężenia światła) działają poprawnie i przesyłają dane do centralnego systemu.
    Użytkownik (rolnik) ma dostęp do dashboardu/aplikacji mobilnej AgroSense.

Główny scenariusz (Basic Flow):

    Zbieranie danych:
    System AgroSense ciągle zbiera dane z czujników monitorujących wilgotność gleby, temperaturę oraz natężenie światła.

    Wykrycie spadku wilgotności:
    Gdy wartość wilgotności gleby spada poniżej ustalonego progu, system generuje automatyczny alert.

    Powiadomienie:
    Alert jest wysyłany do rolnika za pośrednictwem aplikacji mobilnej lub dashboardu webowego.

    Weryfikacja danych:
    Rolnik loguje się do systemu i sprawdza aktualne odczyty z czujników, aby potwierdzić obniżoną wilgotność gleby.

    Decyzja o interwencji:
    Na podstawie otrzymanych informacji rolnik decyduje, czy należy uruchomić system nawadniania.

    Włączenie nawadniania:
    Rolnik wydaje polecenie uruchomienia nawadniania poprzez interfejs systemu.

    Aktywacja systemu:
    System AgroSense wysyła komendę do odpowiedniego urządzenia IoT (sterownika nawadniania), które aktywuje system nawadniania.

    Monitorowanie procesu:
    Podczas nawadniania system kontynuuje monitorowanie odczytów czujników, aby śledzić wzrost wilgotności gleby.

    Zakończenie nawadniania:
    Gdy poziom wilgotności osiągnie optymalny poziom, system automatycznie (lub na polecenie rolnika) dezaktywuje system nawadniania.

    Potwierdzenie wykonania:
    Rolnik otrzymuje informację o zakończeniu procesu nawadniania, a system zapisuje wszystkie zdarzenia w logach.

Scenariusze alternatywne (Alternate Flows):

    Brak komunikacji z czujnikami:
    Jeśli system nie otrzymuje danych z czujników, wysyłany jest komunikat o błędzie, sugerujący sprawdzenie połączeń lub stanu czujników.

    Błąd w aktywacji nawadniania:
    Jeśli system nie może wysłać polecenia lub urządzenie IoT nie odpowiada, rolnik otrzymuje alert o błędzie oraz zalecenia dotyczące dalszej diagnostyki.

Warunki końcowe (Postconditions):

    Gleba jest odpowiednio nawodniona i wilgotność osiągnęła optymalny poziom.
    Wszystkie operacje zostały zarejestrowane w systemie, co umożliwia analizę efektywności nawadniania w przyszłości.
