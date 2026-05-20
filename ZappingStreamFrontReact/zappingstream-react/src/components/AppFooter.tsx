import './AppFooter.css';

export const AppFooter = () => {
    return (
        <div className="footer-section">
            <div className="contact-action-container">
                <p className="contact-email-text">
                    Si tu canal no aparece en la lista y querés sumarlo, o si deseás solicitar la baja de tu contenido, escribinos a<br />
                    <a href="mailto:contacto@zappingstream.com">contacto@zappingstream.com</a>
                </p>
            </div>
            <div className="legal-disclaimer">
                <p>© {new Date().getFullYear()} Zapping Stream. Todos los derechos reservados.</p>
                <p>Al utilizar este sitio, aceptas los <a href="https://www.youtube.com/t/terms" target="_blank" rel="noopener noreferrer">Términos de Servicio de YouTube</a>. Los logos, miniaturas, nombres y descripciones son extraídos directamente de YouTube API Services. Conocé la <a href="https://policies.google.com/privacy" target="_blank" rel="noopener noreferrer" b-43nquhehnb="">Política de Privacidad de Google</a>.
                </p>
                <p>Este sitio es un directorio de canales independiente. No alojamos ni retransmitimos contenido propio. Todos los videos, marcas y logotipos son propiedad exclusiva de sus respectivos creators y se visualizan a través del reproductor oficial de YouTube.</p>
            </div>
        </div>
    );
};
