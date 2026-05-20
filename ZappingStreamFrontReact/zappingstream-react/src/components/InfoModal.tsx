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
                    <p><strong>Zapping Stream</strong> reúne todos los canales de streaming de Argentina en un solo lugar. El objetivo principal es federalizar el contenido...</p>
                    <p>El sitio interactúa directo con YouTube: si se hace click en una tarjeta, el sitio enlaza directo a la transmisión en vivo. Y si en ese momento no están transmitiendo, redirige a su perfil para ver el contenido on-demand.</p>
                    <p>Se puede buscar tanto por nombre del canal como por ciudad. Con esto buscamos darle visibilidad y alentar a los medios pequeños de comunicación a que se animen a armar su propio canal de streaming desde sus ciudades.</p>
                </div>
                <button className="submit-btn" style={{ marginTop: '20px' }} onClick={onClose}>Volver</button>
            </div>
        </div>
    );
};