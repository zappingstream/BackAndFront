# Ciclo de Vida y Reglas de Negocio de los Videos en ZappingStream

Este documento resume las principales decisiones técnicas y lógicas de negocio aplicadas al manejo de streams de YouTube dentro del ecosistema de ZappingStream, principalmente orquestadas por el **LiveChecker** y **ChannelsResync**.

## 1. Estados de los Videos (Modelado)
Los videos en ZappingStream están agrupados dentro de cada canal bajo tres diccionarios principales, que representan su estado de vida:
- **`Upcoming`**: Streams programados para el futuro.
- **`Actives`**: Streams que actualmente están transmitiendo en vivo.
- **`Past`**: Streams que ya finalizaron su transmisión.

Adicionalmente, cada video cuenta con un flag clave llamado **`ToBeCut`** (booleano). Este funciona como un mecanismo de *Soft Delete* (Borrado Lógico) cuando ocurren anomalías en YouTube (ej: un video se pone en privado o es eliminado temporalmente).

---

## 2. LiveChecker: Sincronización de Estados (Vivos y Programados)
El módulo principal que verifica el estado contra la API de YouTube ejecuta periódicamente las siguientes reglas de negocio:

### Sobre los Videos Activos (`Actives`)
- **Desaparición temporal**: Si el video desaparece de la respuesta de YouTube (ej. fue oculto o hubo un error), **no se borra inmediatamente**, sino que se marca con `ToBeCut = true`.
- **Restauración**: Si un video estaba marcado como `ToBeCut = true` pero vuelve a aparecer en la API, se le quita la marca y se restaura (`ToBeCut = false`).
- **Finalización del Vivo**: Si el video en YouTube deja de reportarse como `"live"`, automáticamente es removido de `Actives` y trasladado a la colección **`Past`**. Se documentan sus fechas reales de inicio y finalización.

### Sobre los Videos Programados (`Upcoming`)
- **Paso a En Vivo**: Cuando un evento programado comienza su transmisión (estado `"live"`), es transferido automáticamente de `Upcoming` a **`Actives`**. Si venía marcado como Premiere o tiene duración estática, se respeta la marca `IsPremiere`.
- **Cancelaciones o Desapariciones**: Igual que los activos, si desaparecen, se les aplica el *Soft Delete* (`ToBeCut = true`).
- **Protección contra Glitches**: Si YouTube reporta que un video ya no es `upcoming` ni `live` (estado `"none"`), pero su horario programado está en el futuro, el sistema lo **ignora** asumiendo que el streamer está editando el evento en YT Studio y la API de YouTube está demorada/glitcheada.
- **Limpieza de "Colgados"**: Si un stream `Upcoming` superó su hora de programación inicial hace **más de 24 horas**, el sistema asume que el canal olvidó borrarlo y el evento nunca sucedió. Es eliminado definitivamente de la base de datos.
- **Limpieza de Sin Horario**: Si un stream `Upcoming` no tiene una hora de programación asignada (`scheduledStartTime` es nulo o vacío) y ha permanecido en el sistema por **más de 2 días** (basado en su fecha de agregado o publicación), es eliminado definitivamente.

---

## 3. Mantenimiento y Poda de Videos Pasados (`Past`)
Para no saturar la base de datos de MongoDB con transmisiones muy antiguas, existe un mecanismo automático de poda:
- **Ventana de 7 días**: Se calcula la fecha local (UTC-3 / Hora Argentina) de cuando el video terminó (`EndedAt` o `ActualStartTime`). 
- Si la fecha de finalización supera los **7 días exactos** de antigüedad, el video es **eliminado físicamente** (Poda Física) del historial del canal.

---

## 4. Purga de Descartados (Fantasmas y Soft Deletes)
Las bases de datos acumulan "basura" debido a videos que fueron marcados como `ToBeCut` y que nunca regresaron. El sistema tiene reglas de purga estricta para estos registros:

- **Soft Deletes sin retorno (> 24 horas)**: Si un video en cualquier estado (`Upcoming`, `Actives`, o `Past`) está marcado como `ToBeCut = true` y han pasado más de 24 horas desde su última hora de referencia, se borra de manera definitiva.
- **Videos fantasmas en Past (> 12 horas)**: Para asegurar que los videos en `Past` (que no están marcados para borrado) todavía existan y puedan ser reproducidos, aquellos que finalizaron hace **más de 12 horas** son consultados a la API de YouTube una última vez. **Si ya no existen en YouTube** (ej. el canal lo borró o lo puso privado post-transmisión), es eliminado de la colección `Past`.

---

## 5. Resincronización de Canales (Channels Resync)
Las reglas de los canales también afectan indirectamente a sus videos:
- **Base de Origen**: La lista maestra es la colección `origin`. Todo canal debe tener un ID de YouTube válido (`UC...`).
- **Huérfanos**: Si un canal está en la base de Zapping (`channels`) pero fue removido de la colección maestra (`origin`), el Channels Resync borrará completamente al canal de `channels` (lo que incluye la pérdida definitiva de todos sus diccionarios de videos: Upcoming, Actives y Past).
- **Webhooks**: El sistema renueva periódicamente los Webhooks de YouTube (PubSubHubbub) para todos los canales de `origin`, permitiendo recibir notificaciones en tiempo real (Push) cuando un canal sube contenido o entra en vivo, complementando así al LiveChecker.
