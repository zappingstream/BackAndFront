import './InfoModal.css';

interface InfoModalProps {
    onClose: () => void;
}

export const InfoModal = ({ onClose }: InfoModalProps) => {
    return (
        <div className="modal-overlay" onClick={onClose}>
            <div className="modal-content info-modal" onClick={(e) => e.stopPropagation()}>
                <button className="close-modal-btn" onClick={onClose}>X</button>
                <h3 className="modal-title">¿Qué es Zapping Stream?</h3>
                <div className="info-body">
                    <p><strong>Zapping Stream</strong> reúne todos los canales de streaming de Argentina en un solo lugar. El objetivo principal es federalizar el contenido y hacer accesible todo lo que se está produciendo actualmente en el país.</p>
                    <p>El sitio interactúa directo con YouTube: si se hace click en una tarjeta, el sitio enlaza directo a la transmisión en vivo. Y si en ese momento no están transmitiendo, redirige a su perfil para ver el contenido on-demand.</p>
                    <p>Se puede buscar tanto por nombre del canal como por ciudad. Con esto buscamos darle visibilidad y alentar a los medios pequeños de comunicación a que se animen a armar su propio canal de streaming desde sus ciudades.</p>
                </div>
                
                <div className="modal-contact-section">
                    <p className="contact-email-text">
                        Si tu canal no aparece en la lista y querés sumarlo, o si deseás solicitar la baja de tu contenido, escribinos a<br />
                        <a href="mailto:contacto@zappingstream.com">contacto@zappingstream.com</a>
                    </p>
                </div>
                
                <div className="legal-disclaimer">
                    <p>© {new Date().getFullYear()} Zapping Stream. Todos los derechos reservados.</p>
                    <p>Al utilizar este sitio, aceptas los <a href="https://www.youtube.com/t/terms" target="_blank" rel="noopener noreferrer">Términos de Servicio de YouTube</a>. Los logos, miniaturas, nombres y descripciones son extraídos directamente de YouTube API Services. Conocé la <a href="https://policies.google.com/privacy" target="_blank" rel="noopener noreferrer">Política de Privacidad de Google</a>.</p>
                    <p>Este sitio es un directorio de canales independiente. No alojamos ni retransmitimos contenido propio. Todos los videos, marcas y logotipos son propiedad exclusiva de sus respectivos creadores y se visualizan a través del reproductor oficial de YouTube.</p>
                </div>

                <button className="submit-btn" onClick={onClose}>Volver</button>
            </div>
        </div>
    );
};