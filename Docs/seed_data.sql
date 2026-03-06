-- Seed data для тестирования Ground Control
-- Схема аэропорта с узлами и рёбрами

-- Очистка существующих данных
TRUNCATE TABLE edge_occupancy CASCADE;
TRUNCATE TABLE routes CASCADE;
TRUNCATE TABLE edges CASCADE;
TRUNCATE TABLE nodes CASCADE;

-- Узлы аэропорта
-- Терминалы (T)
INSERT INTO nodes (node_id, x, y, node_type) VALUES
('T-1', 100, 100, 'terminal'),
('T-2', 100, 300, 'terminal');

-- Стоянки (P - Parking)
INSERT INTO nodes (node_id, x, y, node_type) VALUES
('P-1', 300, 100, 'parking'),
('P-2', 300, 200, 'parking'),
('P-3', 300, 300, 'parking'),
('P-4', 500, 100, 'parking'),
('P-5', 500, 200, 'parking'),
('P-6', 500, 300, 'parking');

-- Взлётные полосы (RW - Runway)
INSERT INTO nodes (node_id, x, y, node_type) VALUES
('RW-1', 700, 200, 'runway'),
('RW-2', 900, 200, 'runway');

-- Точки пересечения (J - Junction)
INSERT INTO nodes (node_id, x, y, node_type) VALUES
('J-1', 200, 100, 'junction'),
('J-2', 200, 200, 'junction'),
('J-3', 200, 300, 'junction'),
('J-4', 400, 200, 'junction'),
('J-5', 600, 200, 'junction');

-- Рёбра (дороги между узлами)
-- От терминалов к точкам пересечения
INSERT INTO edges (edge_id, from_node, to_node, length) VALUES
('E-T1-J1', 'T-1', 'J-1', 100),
('E-J1-T1', 'J-1', 'T-1', 100),
('E-T2-J3', 'T-2', 'J-3', 100),
('E-J3-T2', 'J-3', 'T-2', 100);

-- Между точками пересечения (вертикальные)
INSERT INTO edges (edge_id, from_node, to_node, length) VALUES
('E-J1-J2', 'J-1', 'J-2', 100),
('E-J2-J1', 'J-2', 'J-1', 100),
('E-J2-J3', 'J-2', 'J-3', 100),
('E-J3-J2', 'J-3', 'J-2', 100);

-- От точек пересечения к стоянкам
INSERT INTO edges (edge_id, from_node, to_node, length) VALUES
('E-J1-P1', 'J-1', 'P-1', 100),
('E-P1-J1', 'P-1', 'J-1', 100),
('E-J2-P2', 'J-2', 'P-2', 100),
('E-P2-J2', 'P-2', 'J-2', 100),
('E-J3-P3', 'J-3', 'P-3', 100),
('E-P3-J3', 'P-3', 'J-3', 100);

-- Между стоянками и центральной точкой
INSERT INTO edges (edge_id, from_node, to_node, length) VALUES
('E-P1-J4', 'P-1', 'J-4', 100),
('E-J4-P1', 'J-4', 'P-1', 100),
('E-P2-J4', 'P-2', 'J-4', 100),
('E-J4-P2', 'J-4', 'P-2', 100),
('E-P3-J4', 'P-3', 'J-4', 100),
('E-J4-P3', 'J-4', 'P-3', 100);

-- От центральной точки к дальним стоянкам
INSERT INTO edges (edge_id, from_node, to_node, length) VALUES
('E-J4-P4', 'J-4', 'P-4', 100),
('E-P4-J4', 'P-4', 'J-4', 100),
('E-J4-P5', 'J-4', 'P-5', 100),
('E-P5-J4', 'P-5', 'J-4', 100),
('E-J4-P6', 'J-4', 'P-6', 100),
('E-P6-J4', 'P-6', 'J-4', 100);

-- От центральной точки к взлётным полосам
INSERT INTO edges (edge_id, from_node, to_node, length) VALUES
('E-J4-J5', 'J-4', 'J-5', 200),
('E-J5-J4', 'J-5', 'J-4', 200),
('E-J5-RW1', 'J-5', 'RW-1', 100),
('E-RW1-J5', 'RW-1', 'J-5', 100),
('E-RW1-RW2', 'RW-1', 'RW-2', 200),
('E-RW2-RW1', 'RW-2', 'RW-1', 200);

-- Прямые пути между некоторыми стоянками (для альтернативных маршрутов)
INSERT INTO edges (edge_id, from_node, to_node, length) VALUES
('E-P1-P2', 'P-1', 'P-2', 100),
('E-P2-P1', 'P-2', 'P-1', 100),
('E-P2-P3', 'P-2', 'P-3', 100),
('E-P3-P2', 'P-3', 'P-2', 100),
('E-P4-P5', 'P-4', 'P-5', 100),
('E-P5-P4', 'P-5', 'P-4', 100),
('E-P5-P6', 'P-5', 'P-6', 100),
('E-P6-P5', 'P-6', 'P-5', 100);