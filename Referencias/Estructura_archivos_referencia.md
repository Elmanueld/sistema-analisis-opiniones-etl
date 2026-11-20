DOCUMENTO: ESTRUCTURA DE ORGANIZACION DE CARPETAS Y ARCHIVOS
(Instrucciones para el equipo de Infraestructura / Sistemas)

PROPOSITO DE ESTE DOCUMENTO
Este manual detalla los directorios fisicos exactos que deben ser creados en el servidor de Windows ANTES de ejecutar el script de SQL. El motor de base de datos no tiene permisos para crear carpetas nuevas por medidas de seguridad. Si estas carpetas no existen previamente, la instalacion de la base de datos fallara.

PASO 1: CREACION DE CARPETAS (ENTORNO LOCAL O DE PRUEBAS)
Por favor, abrir el explorador de archivos y crear la siguiente estructura desde la raiz de su disco:

Unidad C:

└── Data

├── MDF

├── NDF

└── LDF\

Rutas absolutas que deben quedar disponibles en el sistema:

1. C:\Data\MDF\
2. C:\Data\NDF\
3. C:\Data\LDF\

PASO 2: DISTRIBUCION DE ARCHIVOS (QUE SE GUARDARA EN CADA CARPETA)
Una vez creadas las carpetas, el administrador de base de datos correra el script y los archivos se distribuiran de la siguiente manera:

* Carpeta MDF (Master Data File): Guardara el archivo de datos principal (.mdf) en el filegroup PRIMARY. Almacenara las configuraciones del sistema y las tablas catalogo mas ligeras.
* Carpeta NDF (Secondary Data File): Guardara el archivo de datos secundario (.ndf) en el filegroup SECONDARY. Aqui se enviaran todas las tablas transaccionales pesadas. Esto asegura que los procesos lentos y pesados no bloqueen el archivo principal de la base de datos.
* Carpeta LDF (Log Data File): Guardara el historial de transacciones (.ldf). Se aisla en su propia carpeta para asegurar una rapida recuperacion ante desastres y para no competir por velocidad de lectura/escritura con los archivos de datos.

PASO 3: REGLAS PARA EL ENTORNO DE PRODUCCION (NOTA CRITICA)
Para esta entrega universitaria simulamos la distribucion dentro de una misma carpeta "Data" en el disco C. Sin embargo, en un entorno de produccion real aplica la siguiente regla estricta:

NUNCA se deben guardar archivos de base de datos en el disco C (donde reside el sistema operativo). Si las tablas crecen descontroladamente y el disco C se queda sin espacio, el servidor completo colapsara y se apagara.

Para subir este proyecto a produccion en el futuro, el equipo de infraestructura debera asignar tres discos duros o particiones totalmente independientes:

* Disco D:\ asignado exclusivamente para los archivos MDF.
* Disco E:\ asignado exclusivamente para los archivos NDF.
* Disco F:\ asignado exclusivamente para los archivos LDF.