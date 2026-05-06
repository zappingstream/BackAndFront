## Estructura

La estructura del proyecto es la siguiente

* **ZappingStreamWeb**: La web de zapping stream, que muestra todos los canales disponibles, poniendo en primer lugar de la fila a los que están en vivo o en estreno. En cualquier caso siempre se va a Youtube para verlo con mayor comodidad.
* **ZappingstreamingChannelsResync**: Corre cada 48 hs y se encarga de tomar desde una lista origen todos los canales, obtener los logos y descripciones y luego suscribirse al web hook correspondiente para recibir los avisos de videos
* **ZappingStreamingLiveChecker**: Método de polling activo para videos upcoming y para videos en vivo. Para poner en vivo los upcoming o para borrar a los vivos que ya terminaron recientemente
* **ZappingStreamingapp**: El mismo proyecto que la web, pero para desarrollo mobile. Muy pronto para televisores
