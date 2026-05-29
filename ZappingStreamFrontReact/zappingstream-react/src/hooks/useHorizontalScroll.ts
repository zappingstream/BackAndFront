import { useRef, useEffect } from 'react';
import type {RefObject} from 'react';

export const useHorizontalScroll = <T extends HTMLElement = HTMLDivElement>(externalRef?: RefObject<T>) => {
    const internalRef = useRef<T>(null);
    const elRef = externalRef || internalRef;

    useEffect(() => {
        const el = elRef.current;
        if (!el) return;

        let isDown = false;
        let isDragging = false;
        let startX: number;
        let scrollLeft: number;

        const onMouseDown = (e: MouseEvent) => {
            isDown = true;
            isDragging = false; // Reseteamos la bandera al hacer clic
            startX = e.pageX - el.offsetLeft;
            scrollLeft = el.scrollLeft;
        };

        const onMouseLeave = () => {
            isDown = false;
        };

        const onMouseUp = () => {
            isDown = false;
        };

        const onMouseMove = (e: MouseEvent) => {
            if (!isDown) return;
            e.preventDefault();
            const x = e.pageX - el.offsetLeft;
            const walk = (x - startX) * 2; // Aumenta la velocidad del scroll
            
            // Si se movió más de 5 píxeles, lo consideramos un arrastre
            if (Math.abs(walk) > 5) {
                isDragging = true;
            }
            
            el.scrollLeft = scrollLeft - walk;
        };

        const onClickCapture = (e: MouseEvent) => {
            if (isDragging) {
                e.stopPropagation(); // Evita que el evento llegue a React
                e.preventDefault();  // Evita el comportamiento por defecto (ej. links)
                isDragging = false;  // Reseteamos el estado
            }
        };

        el.addEventListener('mousedown', onMouseDown);
        el.addEventListener('mouseleave', onMouseLeave);
        el.addEventListener('mouseup', onMouseUp);
        el.addEventListener('mousemove', onMouseMove);
        el.addEventListener('click', onClickCapture, true); // `true` atrapa el evento en la fase de captura

        return () => {
            el.removeEventListener('mousedown', onMouseDown);
            el.removeEventListener('mouseleave', onMouseLeave);
            el.removeEventListener('mouseup', onMouseUp);
            el.removeEventListener('mousemove', onMouseMove);
            el.removeEventListener('click', onClickCapture, true);
        };
    }, []);

    return elRef;
};