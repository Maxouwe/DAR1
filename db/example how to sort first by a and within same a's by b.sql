DROP TABLE result;
DROP TABLE B;
DROP TABLE C;

CREATE TABLE B(
  id int,
  a int,
  b int
);


INSERT INTO B
SELECT * FROM A GROUP BY a ORDER BY a;


CREATE TABLE C(
  id int,
  a int,
  b int
);


INSERT INTO C
SELECT * FROM A ORDER BY b;

CREATE TABLE result(
  id int,
  a int,
  b int
);

INSERT INTO result
SELECT A.id, Q.a, Q.b FROM
(SELECT B.a, C.b FROM B
 INNER JOIN C ON B.a = C.a) AS Q
INNER JOIN A ON Q.a = A.a AND Q.b = A.b