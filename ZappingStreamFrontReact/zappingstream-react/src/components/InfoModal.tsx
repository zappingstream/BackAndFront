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
                    <p><strong>Zapping Stream</strong> reúne todos los canales de streaming de Argentina en un solo lugar. El objetivo es brindarle a la comunidad un punto de acceso de descubrimiento de canales y contenido por fuera del algoritmo.</p>
                    <p>El sitio interactúa directo con YouTube: si se hace click en una tarjeta, el sitio enlaza directo al video o canal de Youtube. El sitio también permite buscar canales por nombre o ciudad.</p>
                    <p>Se puede visualizar por canal o por transmisiones programadas. Nuestro propósito es facilitar el descubrimiento de contenido local y promover la visibilidad de todos los creadores y medios independientes.</p>
                </div>
                
                <div className="modal-contact-section">
                    <p className="contact-email-text">
                        Si tu canal no aparece en la lista y querés sumarlo, o si deseás solicitar la baja de tu contenido, escribinos a<br />
                        <a href="mailto:contacto@zappingstream.com">contacto@zappingstream.com</a>
                    </p>
                </div>
                
                <div className="legal-disclaimer">
x                    <p>Al utilizar este sitio, aceptas los <a href="https://www.youtube.com/t/terms" target="_blank" rel="noopener noreferrer">Términos de Servicio de YouTube</a>. Los logos, miniaturas, nombres y descripciones son extraídos directamente de YouTube API Services. Conocé la <a href="https://policies.google.com/privacy" target="_blank" rel="noopener noreferrer">Política de Privacidad de Google</a>.</p>
                    <p>Este sitio es un directorio de canales independiente. No alojamos ni retransmitimos contenido propio. Todos los videos, marcas y logotipos son propiedad exclusiva de sus respectivos creadores y se visualizan a través del reproductor oficial de YouTube.</p>
                </div>

                <button className="submit-btn" onClick={onClose}>Volver</button>
            </div>
        </div>
    );
};