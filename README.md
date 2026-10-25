Nexo Support Toolkit

Nexo Support Toolkit (NexoSupportToolKit) es una aplicación de escritorio para Windows desarrollada en C# con Windows Forms. Reúne en un solo lugar herramientas de limpieza, optimización, diagnóstico y creación de USB booteables para mantener equipos en buen estado.

El proyecto nació como una herramienta de limpieza automática para evitar la acumulación de archivos innecesarios y las limpiezas manuales periódicas. Con el tiempo creció hasta convertirse en una suite de mantenimiento y sigue sumando funciones.

Herramientas principales

🧹 Limpieza

* Limpieza configurable: permite elegir la carpeta a mantener y la antigüedad de los archivos a eliminar (3 meses, 6 meses o 1 año).
* Automatización: programa limpiezas periódicas mediante el Programador de tareas de Windows.
* Limpieza manual y de archivos temporales de Windows y del usuario.
* Aviso en la bandeja del sistema cuando hay archivos que alcanzaron la antigüedad configurada.
* Registro de operaciones y errores.
* Protecciones para evitar la limpieza de ubicaciones críticas del sistema.

⚙️ Optimización de Windows

* Optimización de unidades HDD y SSD.
* Reparación de Windows con DISM y SFC.
* Acceso rápido al sensor de almacenamiento y a las aplicaciones de inicio.
* Vaciado de la papelera de reciclaje con verificación del resultado.

📊 Diagnóstico y monitoreo

* Uso en tiempo real del procesador, la memoria RAM, la tarjeta gráfica (GPU) y la unidad del sistema, con actualización automática cada segundo.
* Informe de componentes del equipo: procesador, placa madre, memoria RAM, tarjeta gráfica, discos y BIOS, con opción de copiarlo al portapapeles.

💾 Creador de USB booteable

* Detección de memorias USB conectadas, con letra, capacidad y sistema de archivos. Nunca se ofrece el disco del sistema.
* Selección de la imagen ISO (Windows 10, Windows 11 o distribuciones Linux).
* Configuración de arranque: esquema de partición GPT o MBR, sistema de destino UEFI o BIOS, sistema de archivos, tamaño de clúster y etiqueta del volumen.
* Dos modos según la ISO: copia de archivos para imágenes de instalación de Windows (con división automática de install.wim cuando supera el límite de 4 GB de FAT32) y escritura directa para imágenes híbridas, típicas de Linux.
* Barra de progreso, registro de la operación y botón para cancelar.
* Confirmación obligatoria antes de borrar la unidad: hay que escribir BORRAR.

Atención: crear un USB booteable borra todos los datos de la unidad seleccionada.

Requisitos

* Windows 10 u 11.
* .NET con soporte para Windows Forms (ver el archivo del proyecto para la versión).
* Permisos de administrador para el creador de USB, la optimización de unidades y la reparación con DISM y SFC.

Tecnologías utilizadas

* C#
* .NET
* Windows Forms
* WMI (System.Management)
* PowerShell, diskpart y DISM
* Windows Task Scheduler

Estado del proyecto

En desarrollo activo. Se seguirán incorporando nuevas herramientas de mantenimiento, diagnóstico y administración del equipo.

Objetivo del proyecto

Ofrecer una herramienta liviana y configurable que automatice el mantenimiento de equipos Windows, reduzca tareas repetitivas y concentre en una sola aplicación las utilidades que un técnico usa a diario.

Licencia

Este proyecto se distribuye bajo GNU General Public License, versión 3 o cualquier versión posterior, a elección de quien lo reciba (GPL-3.0-or-later). Se puede usar, estudiar, modificar y redistribuir conforme a sus términos. Las versiones derivadas que se distribuyan deben conservar esta misma libertad y cumplir la GPL. El texto completo está en el archivo LICENSE.

La aplicación no requiere una clave de activación para iniciar.
