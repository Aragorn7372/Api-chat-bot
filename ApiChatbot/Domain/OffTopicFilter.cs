namespace ApiChatbot.Domain;

/// <summary>
/// Filtro de mensajes fuera del alcance del portafolio.
/// Contiene categorías de palabras clave en español para detectar mensajes
/// sobre deportes, política, entretenimiento, animales, cocina, medicina,
/// religión, clima, moda, gaming y redes sociales.
/// </summary>
public class OffTopicFilter
{
    private readonly string _name;
    private readonly string _rejectionMessage;

    private static readonly string[][] OffTopicKeywords =
    [
        ["fútbol", "baloncesto", "tenis", "champions", "liga", "mundial", "partido", "jugador", "deporte", "copa", "campeonato", "gol", "estadio", "entrenador", "árbitro", "selección", "nba", "nfl"],
        ["gobierno", "presidente", "partido político", "elecciones", "voto", "política", "ministro", "senado", "congreso", "alcalde", "diputado"],
        ["película", "serie", "música", "cantante", "actor", "famoso", "celebridad", "videojuego", "pokemon", "netflix", "disney", "spotify", "canción", "álbum", "cine", "tv"],
        ["perro", "gato", "animal", "mascota", "caballo", "pez", "pájaro", "charmander", "pokémon", "dinosaurio", "tortuga"],
        ["receta", "cocina", "plato", "ingrediente", "restaurante", "comida", "bebida", "cocinar", "chef", "cena", "desayuno"],
        ["enfermedad", "medicina", "doctor", "hospital", "salud", "tratamiento", "síntoma", "paciente", "cirugía", "virus", "bacteria"],
        ["dios", "iglesia", "religión", "creencia", "fe", "rezar", "oración", "biblia", "cristo", "virgen"],
        ["clima", "lluvia", "nieve", "tormenta", "terremoto", "huracán", "temperatura", "pronóstico", "meteorología"],
        ["receta", "moda", "ropa", "zapatos", "maquillaje", "belleza", "corte", "peinado"],
        ["acción", "aventura", "rol", "estrategia", "simulación", "multijugador", "nintendo", "playstation", "xbox", "steam"],
        ["youtube", "tiktok", "instagram", "twitter", "influencer", "streamer", "viral"],
    ];

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="OffTopicFilter"/>.
    /// </summary>
    /// <param name="name">Nombre del dueño del portafolio (se usa en el mensaje de rechazo).</param>
    public OffTopicFilter(string name)
    {
        _name = name;
        _rejectionMessage = $"Lo siento, solo puedo brindar información sobre el portafolio de {_name}. Si tienes preguntas sobre su formación, proyectos, habilidades técnicas o experiencia laboral, estaré encantado de ayudarte.";
    }

    /// <summary>
    /// Evalúa si un mensaje contiene temas fuera del alcance del portafolio.
    /// </summary>
    /// <param name="message">Mensaje del usuario a evaluar.</param>
    /// <returns><c>true</c> si el mensaje es off-topic; <c>false</c> si está dentro del alcance.</returns>
    public bool IsOffTopic(string message)
    {
        var lower = message.ToLowerInvariant();
        return OffTopicKeywords.Any(category => category.Any(keyword => lower.Contains(keyword)));
    }

    /// <summary>
    /// Mensaje de rechazo amigable cuando se detecta un tema fuera de alcance.
    /// </summary>
    public string RejectionMessage => _rejectionMessage;
}
