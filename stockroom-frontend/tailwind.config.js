/** @type {import('tailwindcss').Config} */
export default { content: ['./index.html', './src/**/*.{js,jsx}'], theme: { extend: { fontFamily: { sans: ['Inter', 'ui-sans-serif', 'system-ui'] }, colors: { ink: '#17211d', muted: '#78837d', line: '#e8ece9', canvas: '#f6f7f5', green: '#21845b' }, boxShadow: { card: '0 2px 12px rgba(27, 43, 35, .045)' } } }, plugins: [] }
