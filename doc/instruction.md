
Spela
Instruktioner
Instruktioner
Översikt
I den här spelsimulatorn kan man välja instruktörer för de respektive lagen. Om du väljer datorstyrd så kommer laget styras av en inbyggd programmerad spelare. Väljer du att registrera ett nytt lag, skriver du in en url och väljer ladda för att hämta namn och färger för laget och då ser du också att du har kontakt. Lag som registreras sparas i aktuell webbläsare tillsvidare.

Bygga ett API
Allmänt
Om du vill programmera ett eget lag så får du bygga och publiera ett API. Detta API behöver ha 2 endpoints (setup (GET), update (POST)). En för att ge namn och färger för laget (setup) och en för att ge laget instruktioner (update). Tänk på att ställa in CORS så att denna simulator kan anropa API't.

Data
I kommunikationen med API't så får och ger API't data utifrån att det är hemmalag (spelar på vänster sida). Simulatorn spegelvänder datat i X-led automatiskt. Det kan vara lite missledande eftersom vänsterback/vänsterforward spelar på högersidan som bortalag men det påverkar ju in någon logik men kan vara bra att veta om man har olika spelstil på höger och vänster sida.

Enheter i datat är centimeter, sekunder och cm/s. Planen är 12000*9000 cm där övre vänstra hörnet är 0,0.

Resurser
Gränsnittet för ditt API hittar du här som en swagger: Swagger.json.

