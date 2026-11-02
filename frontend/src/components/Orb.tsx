import { Canvas } from '@react-three/fiber';
import { Float, MeshDistortMaterial, Sphere, Stars } from '@react-three/drei';

export default function Orb() {
  return (
    <Canvas dpr={[1, 1.5]} camera={{ position: [0, 0, 4] }}>
      <ambientLight intensity={0.6} />
      <pointLight position={[5, 5, 5]} intensity={40} color="#8b5cf6" />
      <pointLight position={[-5, -3, 2]} intensity={30} color="#22d3ee" />
      <Float speed={1.5}>
        <Sphere args={[1.3, 64, 64]} position={[1.4, 0, 0]}>
          <MeshDistortMaterial color="#5b6cff" distort={0.45} speed={2} roughness={0.2} metalness={0.6} />
        </Sphere>
      </Float>
      <Stars radius={30} depth={30} count={2500} factor={3} fade speed={1} />
    </Canvas>
  );
}
